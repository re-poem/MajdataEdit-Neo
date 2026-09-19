using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_Neo.Types;
using MajdataEdit_Neo.Types.Plugin;
using MajdataEdit_Neo.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Types;
using static MajdataEdit_Neo.Base.MajEnv;

namespace MajdataEdit_Neo.ViewModels;

/// <summary>
/// Composition root: holds and coordinates all editor state, provides window-level UI state.
/// Split into partial files by videoNames: Document.cs, FileSession.cs, Playback.cs, Tools.cs,
/// AutoSave.cs, DiscordRpc.cs, Plugin.cs, Settings.cs, Update.cs.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    public static MainWindowViewModel Ins { get; private set; } = null!;
    private ShortcutWindow? _shortcutWindow;

    //------status bar (window-level)

    [ObservableProperty]
    private string? _statusBarMessage = null;

    //------derived properties that span multiple models

    public string WindowTitle
    {
        get
        {
            var baseTitle = $"MajdataEdit Neo {MAJDATA_VERSION_STRING}";
            if (CurrentMaidata.IsEmpty) return baseTitle;
            return baseTitle + WindowTitleSuffix;
        }
    }

    public bool IsPointerPressedSimaiVisual { get; set; }

    //------constructor

    public MainWindowViewModel()
    {
        Ins = this;

        InitializeDocument();
        InitializePlayback();
        InitializeAutoSave();
        InitializePlugins();

        // Wire document -> window title / auto-save / playback events
        WireEvents();

        // Initialize settings (may signal to open settings window)
        BackgroundImage = _emptyBitmap;
        var needsSettingsWindow = InitializeSettings();
        if (needsSettingsWindow)
        {
            OpenSettingsWindow();
        }

        InitializeDiscordRpc();

        // Design-time support
        if (Design.IsDesignMode)
        {
            CurrentMaidata = MaidataFile.Parse("&title=\n&artist=\n&first=0\n");
        }
    }

    public void NotifyWindowTitleChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
    }

    //------window-level methods

    public void ShowStatusMessage(string message) => StatusBarMessage = message;
    public void ResetStatusMessage() => StatusBarMessage = null;

    public async Task OnWindowClosingAsync()
    {
        _shortcutWindow?.Close();
        _shortcutWindow = null;

        try
        {
            SaveEditRecord();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save edit record while closing: {ex}");
        }

        await DisposeAsync();
        try
        {
            DisposeSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to dispose settings resources: {ex}");
        }
        _editDb.Dispose();
    }

    [RelayCommand]
    public void AboutButtonClicked(string? index)
    {
        switch (index)
        {
            case "0": OpenBrowser("https://discord.gg/AcWgZN7j6K"); break;
            case "1": OpenBrowser("https://qm.qq.com/q/GAxbFZHP6A"); break;
            case "2": OpenBrowser("https://github.com/LingFeng-bbben/MajdataEdit-Neo"); break;
            case "3": OpenBrowser("https://github.com/re-poem/MajdataViewX"); break;
            case "4": OpenBrowser("https://majdata.net/"); break;
        }
        static void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
    }

    [RelayCommand]
    public void OpenShortcutWindow()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow is null) return;

        if (_shortcutWindow is not null)
        {
            _shortcutWindow.Activate();
            return;
        }

        var window = new ShortcutWindow
        {
            DataContext = new ShortcutWindowViewModel()
        };
        _shortcutWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_shortcutWindow, window))
                _shortcutWindow = null;
        };
        window.Show(desktop.MainWindow);
    }

    public async void OpenSettingsWindow()
    {
        if (Application.Current?.ApplicationLifetime is not
            IClassicDesktopStyleApplicationLifetime mainWindow ||
            mainWindow.MainWindow is null) return;

        var settingsViewModel = new SettingsViewModel();
        settingsViewModel.LoadSettings(Settings);
        var window = new SettingsWindow
        {
            DataContext = settingsViewModel
        };
        await window.ShowDialog(mainWindow.MainWindow);
        SaveSettings();
        await Task.Delay(1);
    }

    public async void OpenChartInfoWindow()
    {
        if (CurrentMaidata.IsEmpty) return;
        var mainWindow = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (mainWindow is null || mainWindow.MainWindow is null) return;
        using var chartInfo = new ChartInfoViewModel()
        {
            Title = CurrentMaidata.Title,
            Artist = CurrentMaidata.Artist,
            FinalDesigner = CurrentMaidata.Designer,
            SimaiCommands = [.. CurrentMaidata.Commands.Select(c => new MutSimaiCommand(c.Key, c.Value))],
            MaidataDir = MaidataDir
        };
        var window = new ChartInfoWindow
        {
            DataContext = chartInfo
        };
        await window.ShowDialog(mainWindow.MainWindow);
        var datacontext = window.DataContext as ChartInfoViewModel;
        if (datacontext is null)
            throw new InvalidOperationException("Chart info window has an unexpected data context.");

        // 直接写 MaidataFile 的字段，没有 init-only，没有 native handle 要保活。
        CurrentMaidata.Title = datacontext.Title ?? string.Empty;
        CurrentMaidata.Artist = datacontext.Artist ?? string.Empty;
        CurrentMaidata.Designer = datacontext.FinalDesigner ?? string.Empty;
        CurrentMaidata.Commands.Clear();
        foreach (var c in datacontext.SimaiCommands)
            CurrentMaidata.Commands.Add(new MutSimaiCommand(c.Key, c.Value));

        await Task.Delay(100);
        NotifySimaiFileChanged();
    }

    public async void OpenRecoverWindow()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow is null) return;

        var maidataDirectory = IsLoaded ? MaidataDir : null;
        var recoverViewModel = await RecoverViewModel.CreateAsync(this, maidataDirectory);
        var window = new RecoverWindow(recoverViewModel);
        var result = await window.ShowDialog<RecoverDialogResult?>(desktop.MainWindow);
        if (result is null) return;

        if (result.Action == RecoverDialogAction.Load)
        {
            await OpenFile(result.MaidataPath);
            return;
        }

        if (!IsLoaded) return;
        var currentMaidataPath = Path.GetFullPath(Path.Combine(MaidataDir, "maidata.txt"));
        var recoveredMaidataPath = Path.GetFullPath(result.MaidataPath);
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(
            currentMaidataPath,
            recoveredMaidataPath,
            pathComparison))
        {
            await ReloadFile();
        }
    }

    public void OpenBpmTapWindow()
    {
        new BpmTapWindow().Show();
    }

    public event Action<PluginAction>? RequestPluginActionExecution;
    [RelayCommand]
    public void ExecutePluginAction(PluginAction action)
    {
        RequestPluginActionExecution?.Invoke(action);
    }
}
