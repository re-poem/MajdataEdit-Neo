using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_Neo.Assets.Langs;
using MajdataEdit_Neo.Models;
using MajdataEdit_Neo.Types;
using MajdataEdit_Neo.Utils;
using MajdataEdit_Neo.Views;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Types;

namespace MajdataEdit_Neo.ViewModels;

partial class ChartInfoViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private string? title;
    [ObservableProperty]
    private string? artist;
    [ObservableProperty]
    private string? finalDesigner;

    [ObservableProperty]
    private ObservableCollection<MutSimaiCommand> simaiCommands = new();

    [ObservableProperty]
    private string? maidataDir;

    [ObservableProperty]
    private Bitmap? cover;

    partial void OnMaidataDirChanged(string? value)
    {
        ReloadCover();
    }

    public void AddNewCommand()
    {
        SimaiCommands.Add(new MutSimaiCommand("prefix", "value"));
    }
    public void DelCommand(MutSimaiCommand command)
    {
        if (SimaiCommands is null) throw new InvalidOperationException();
        SimaiCommands.Remove(command);
    }

    public async Task OpenBgCover()
    {
        string? tempPath = null;
        try
        {
            var file = await FileIOManager.DoOpenFilePickerAsync(FileIOManager.FileOpenerType.Image);
            if (file is null) return;
            var path = file.TryGetLocalPath();
            if (path is null || MaidataDir is null) return;

            var extension = Path.GetExtension(path);
            var isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
            var isJpeg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
            if (!isPng && !isJpeg)
                return;

            var targetPath = Path.Combine(MaidataDir, isPng ? "bg.png" : "bg.jpg");
            var otherPath = Path.Combine(MaidataDir, isPng ? "bg.jpg" : "bg.png");
            var pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!Path.GetFullPath(path).Equals(Path.GetFullPath(targetPath), pathComparison))
            {
                tempPath = Path.Combine(
                    MaidataDir,
                    $".bg.{Guid.NewGuid():N}{(isPng ? ".png" : ".jpg")}.tmp");
                File.Copy(path, tempPath);

                DisposeCover();
                File.Move(tempPath, targetPath, overwrite: true);
                tempPath = null;
            }

            if (File.Exists(otherPath))
                File.Delete(otherPath);
            ReloadCover();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            ReloadCover();
        }
        finally
        {
            if (tempPath is not null && File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public async Task OpenBgVideo()
    {
        try
        {
            var file = await FileIOManager.DoOpenFilePickerAsync(FileIOManager.FileOpenerType.Video);
            if (file is null) return;
            var path = file.TryGetLocalPath();
            if (path is null || MaidataDir is null) return;
            File.Delete(MaidataDir + "/bg.mp4");
            File.Delete(MaidataDir + "/pv.mp4");
            File.Copy(path, MaidataDir + "/bg.mp4", true);
            if (new FileInfo(path).Length > 20971520)
            {
                var result = await MessageBox.ShowWindowDialogAsync(Langs.Msg_BgTooLarge, Langs.Gui_Warning,
                    MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning);
                if (result == MsBox.Avalonia.Enums.ButtonResult.Yes)
                {
                    await MainWindowViewModel.Ins.CompressBgVideoAsync();
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private void ReloadCover()
    {
        DisposeCover();
        if (MaidataDir is not null)
        {
            var coverPath = Path.Combine(MaidataDir, "bg.png");
            if (File.Exists(coverPath))
            {
                Cover = new Bitmap(coverPath);
                return;
            }

            coverPath = Path.Combine(MaidataDir, "bg.jpg");
            if (File.Exists(coverPath))
            {
                Cover = new Bitmap(coverPath);
                return;
            }
        }

        using var stream = AssetLoader.Open(
            new Uri("avares://MajdataEdit-Neo/Assets/dummy.png"));
        Cover = new Bitmap(stream);
    }

    private void DisposeCover()
    {
        var previous = Cover;
        Cover = null;
        previous?.Dispose();
    }

    public void Dispose()
    {
        DisposeCover();
    }
}
