using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_Neo.Types.MajSetting;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using static MajdataEdit_Neo.Base.MajEnv;

namespace MajdataEdit_Neo.ViewModels;

/// <summary>
/// 设置加载/保存/应用
/// </summary>
public partial class MainWindowViewModel
{
    //reload setting required
    [ObservableProperty] public partial MajSetting Settings { get; set; } = null!;
    [ObservableProperty] public partial double FontSize { get; set; }
    [ObservableProperty] public partial bool IsAnimated { get; set; }
    [ObservableProperty] public partial Bitmap BackgroundImage { get; set; }
    [ObservableProperty] public partial bool WordWrap { get; set; }

    private readonly WriteableBitmap _emptyBitmap =
        new(new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888);
    private string? _loadedBackgroundPath;

    private bool InitializeSettings()
    {
        BackgroundImage = _emptyBitmap;
        if (!File.Exists(SettingsFile))
        {
            CreateSettings();
            return true;
        }
        ReadSettings();
        return false;
    }

    private void CreateSettings()
    {
        Settings = new MajSetting();
        File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(Settings, Formatting.Indented));
        ReloadSettings();
    }

    private void ReadSettings()
    {
        var json = File.ReadAllText(SettingsFile);
        Settings = JsonConvert.DeserializeObject<MajSetting>(json)!;
        ReloadSettings();
        SaveSettings();
    }

    public void ReloadSettings(bool update = false)
    {
        I18N.Ins.Culture = new CultureInfo(Settings.EditSetting.Language);
        FontSize = Settings.EditSetting.FontSize;
        IsAnimated = Settings.EditSetting.WaveAnimated;
        var bgPath = GetPath(Settings.EditSetting.BackgroundImagePath);
        var normalizedPath = File.Exists(bgPath) ? Path.GetFullPath(bgPath) : null;
        if (!string.Equals(_loadedBackgroundPath, normalizedPath, StringComparison.Ordinal))
        {
            var previous = BackgroundImage;
            BackgroundImage = normalizedPath is null
                ? _emptyBitmap
                : new Bitmap(normalizedPath);
            _loadedBackgroundPath = normalizedPath;
            if (!ReferenceEquals(previous, _emptyBitmap))
                previous.Dispose();
        }
        WordWrap = Settings.EditSetting.WordWrap;

        _ = _playerConnection.SettingAsync(Settings.ViewSetting, Settings.VolumeSetting);
        if (update)
        {
            var file = CurrentMaidata!;
            var chartText = file.Fumens[SelectedDifficulty] ?? string.Empty;
            _ = _playerConnection.UpdateAsync(file, SelectedDifficulty, chartText,
                file.Levels[SelectedDifficulty] ?? "", file.Designers[SelectedDifficulty] ?? "");
        }
    }

    public void SetWindowLastState(Window window)
    {
        Settings.WindowSetting = new MajWindowSetting
        {
            Width = window.Bounds.Width,
            Height = window.Bounds.Height,
            PosX = window.Position.X,
            PosY = window.Position.Y
        };
        SaveSettings();
    }

    public void SaveSettings()
    {
        File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(Settings, Formatting.Indented));
    }

    private void DisposeSettings()
    {
        if (!ReferenceEquals(BackgroundImage, _emptyBitmap))
            BackgroundImage.Dispose();
        _emptyBitmap.Dispose();
    }
}
