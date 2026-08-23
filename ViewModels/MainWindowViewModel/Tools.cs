using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_Neo.Assets.Langs;
using MajdataEdit_Neo.Models.TrackUtils;
using MajdataEdit_Neo.Utils;
using MsBox.Avalonia.Enums;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static MajdataEdit_Neo.Utils.FFmpegChecker;

namespace MajdataEdit_Neo.ViewModels;

/// <summary>
/// 工具：背景视频压缩、媒体对齐、从视频新建谱面
/// </summary>
public partial class MainWindowViewModel
{
    [ObservableProperty]
    public partial int MediaQuickProcessBeatsCount { get; set; } = 4;

    [ObservableProperty]
    public partial bool MediaQuickProcessFreezeFrame { get; set; } = false;

    private static readonly string[] VideoFilter = ["*.mp4", "*.mkv", "*.avi", "*.mov", "*.flv", "*.wmv"];
    private static readonly string[] AllFilter = ["*.*"];
    private readonly object _ffmpegSync = new();
    private CancellationTokenSource? _ffmpegCts;

    [RelayCommand]
    public Task CompressBgVideo() => CompressBgVideoAsync();

    [RelayCommand]
    public Task MediaQuickProcess() => MediaQuickProcessAsync();

    [RelayCommand]
    public Task NewChartFromVideo() => NewChartFromVideoAsync();

    public bool CancelCurrentFFmpeg()
    {
        lock (_ffmpegSync)
        {
            if (_ffmpegCts is null || _ffmpegCts.IsCancellationRequested)
                return false;

            _ffmpegCts.Cancel();
            return true;
        }
    }

    public async Task CompressBgVideoAsync()
    {
        var maidataDir = MaidataDir;
        var bgVideoPath = Path.Combine(maidataDir, "bg.mp4");
        if (!File.Exists(bgVideoPath))
        {
            bgVideoPath = Path.Combine(maidataDir, "pv.mp4");
        }
        if (!File.Exists(bgVideoPath))
        {
            await MessageBox.ShowWindowDialogAsync(Langs.Status_NoBgVideo, "Error", icon: MsBox.Avalonia.Enums.Icon.Error);
            return;
        }

        if (!await EnsureFFmpeg()) return;

        var videoBaseName = Path.GetFileNameWithoutExtension(bgVideoPath);
        var outputPath = Path.Combine(maidataDir, $"{videoBaseName}_compressed.mp4");
        var operation = BeginFfmpegOperation();

        ShowStatusMessage($"{Langs.Status_Compressing}");

        try
        {
            var completed = await Task.Run(
                () => TrackProcessor.CompressVideo(ExecutablePath, bgVideoPath, outputPath, operation.Token),
                operation.Token);
            if (!completed)
            {
                Debug.WriteLine("FFmpeg video compression was cancelled.");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                return;
            }

            if (File.Exists(outputPath))
            {
                var backupPath = Path.Combine(maidataDir, $"{videoBaseName}_original.mp4");
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Move(bgVideoPath, backupPath);
                File.Move(outputPath, bgVideoPath);

                var originalSize = new FileInfo(backupPath).Length / 1024.0 / 1024.0;
                var newSize = new FileInfo(bgVideoPath).Length / 1024.0 / 1024.0;

                await MessageBox.ShowWindowDialogAsync(
                    $"{Langs.Status_CompressComplete}\n{originalSize:F2}MB �?{newSize:F2}MB",
                    "Success", icon: MsBox.Avalonia.Enums.Icon.Success);
                await ReloadFile();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("FFmpeg video compression was cancelled.");
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            await MessageBox.ShowWindowDialogAsync($"Error: {ex.Message}", "Error", icon: MsBox.Avalonia.Enums.Icon.Error);
        }
        finally
        {
            if (CompleteFfmpegOperation(operation))
                ResetStatusMessage();
        }
    }

    public async Task MediaQuickProcessAsync()
    {
        CancellationTokenSource? operation = null;
        try
        {
            if (CurrentChartData is null || CurrentChartData.CommaTimings.Length == 0)
            {
                await MessageBox.ShowWindowDialogAsync(
                    Langs.Msg_NoBpmInChart,
                    Langs.Gui_Error,
                    ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                return;
            }
            var firstTiming = CurrentChartData.CommaTimings[0];
            var bpm = firstTiming.Bpm; var offset = Offset;
            var beatsCount = MediaQuickProcessBeatsCount; var freezeFrame = MediaQuickProcessFreezeFrame;
            if (!await EnsureFFmpeg()) return;
            var maidataDir = MaidataDir;
            var audioPath = Path.Combine(maidataDir, "track.mp3");
            if (!File.Exists(audioPath)) audioPath = Path.Combine(maidataDir, "track.ogg");
            operation = BeginFfmpegOperation();
            ShowStatusMessage($"{Langs.Status_Processing}");
            var completed = await Task.Run(() =>
            {
                if (!TrackProcessor.AdjustMediaTime(
                    ExecutablePath,
                    audioPath,
                    60.0 / bpm * beatsCount,
                    offset,
                    cancellationToken: operation.Token))
                    return false;

                string? videoPath = null;
                foreach (var name in new[] { "pv.mp4", "mv.mp4", "bg.mp4" })
                {
                    var dir = Path.Combine(maidataDir, name);
                    if (File.Exists(dir))
                    {
                        videoPath = dir;
                        break;
                    }
                }
                if (videoPath != null)
                    return TrackProcessor.AdjustMediaTime(
                        ExecutablePath,
                        videoPath,
                        60.0 / bpm * beatsCount,
                        offset,
                        freezeFrame,
                        operation.Token);
                return true;
            }, operation.Token);
            if (!completed)
            {
                Debug.WriteLine("FFmpeg media processing was cancelled.");
                return;
            }

            Offset = 0;
            await SaveFile();
            await Task.Delay(30);
            await ReloadFile();
            await MessageBox.ShowWindowDialogAsync(Langs.Msg_MediaProcessComplete, Langs.Gui_Success, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Success);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("FFmpeg media processing was cancelled.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            await MessageBox.ShowWindowDialogAsync(string.Format(Langs.Msg_MediaProcessFailed, ex.Message), Langs.Gui_Error, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
        }
        finally
        {
            if (operation is not null && CompleteFfmpegOperation(operation))
                ResetStatusMessage();
        }
    }

    public async Task NewChartFromVideoAsync()
    {
        CancellationTokenSource? operation = null;
        string? audioTempPath = null;
        try
        {
            var files = await StorageUtils.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Video File",
                FileTypeFilter = [
                    new FilePickerFileType("Video Files") { Patterns = VideoFilter },
                    new FilePickerFileType("All Files") { Patterns = AllFilter }
                ],
                AllowMultiple = false
            });
            if (files == null || files.Count == 0) return;
            var file = files[0].TryGetLocalPath();
            if (file is null) return;
            var parent = Path.GetDirectoryName(file)!;
            var newFile = Path.Combine(parent, "pv.mp4");
            if (file != newFile)
            {
                if (File.Exists(newFile))
                    File.Delete(newFile); File.Move(file, newFile);
            }
            if (!await EnsureFFmpeg()) return;
            operation = BeginFfmpegOperation();
            ShowStatusMessage($"{Langs.Status_ExtractingAudio}");
            var audioPath = Path.Combine(parent, "track.mp3");
            audioTempPath = Path.Combine(parent, $".track.{Guid.NewGuid():N}.tmp.mp3");
            var completed = await Task.Run(
                () => TrackProcessor.ExtractAudio(ExecutablePath, newFile, audioTempPath, operation.Token),
                operation.Token);
            if (!completed)
            {
                Debug.WriteLine("FFmpeg audio extraction was cancelled.");
                return;
            }

            File.Move(audioTempPath, audioPath, overwrite: true);
            audioTempPath = null;
            await NewChartFromDir(parent);
            OpenChartInfoWindow();
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("FFmpeg audio extraction was cancelled.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            await MessageBox.ShowWindowDialogAsync(string.Format(Langs.Msg_ExtractAudioFailed, ex.Message), Langs.Gui_Error, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
        }
        finally
        {
            if (audioTempPath is not null && File.Exists(audioTempPath))
                File.Delete(audioTempPath);
            if (operation is not null && CompleteFfmpegOperation(operation))
                ResetStatusMessage();
        }
    }

    private CancellationTokenSource BeginFfmpegOperation()
    {
        lock (_ffmpegSync)
        {
            _ffmpegCts?.Cancel();
            _ffmpegCts = new CancellationTokenSource();
            return _ffmpegCts;
        }
    }

    private bool CompleteFfmpegOperation(CancellationTokenSource operation)
    {
        lock (_ffmpegSync)
        {
            var isCurrent = ReferenceEquals(_ffmpegCts, operation);
            if (isCurrent)
                _ffmpegCts = null;
            operation.Dispose();
            return isCurrent;
        }
    }
}
