using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_Neo.Assets.Langs;
using MajdataEdit_Neo.Models;
using MajdataEdit_Neo.Types;
using MajdataEdit_Neo.Utils;
using MsBox.Avalonia.Enums;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static MajdataEdit_Neo.Base.MajEnv;

namespace MajdataEdit_Neo.ViewModels;

/// <summary>
/// 文件会话：打开/新建/保存谱面、编辑记录持久化
/// </summary>
public partial class MainWindowViewModel
{
    //------file state
    private readonly ChartEditDatabase _editDb = new(DatabaseFile);
    readonly TrackReader _trackReader = new();
    readonly EditTimer _editTimer = new();

    [ObservableProperty]
    private string _maidataDir = "";

    [ObservableProperty]
    private TrackInfo? _songTrackInfo = null;

    //------event wiring

    private void WireEvents()
    {
        // Doc changes -> update WindowTitle, auto-save, stop playback
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CurrentMaidata))
            {
                NotifyWindowTitleChanged();
                Stop(false);
                UpdateFumenContextChanged();
                IsFileChanged = !IsSaved;
                _ = OnSimaiFileChangedAsync(CurrentMaidata);
            }
            else if (e.PropertyName == nameof(SelectedDifficulty))
            {
                SaveEditRecord();
            }
            else if (e.PropertyName == nameof(IsSaved))
            {
                NotifyWindowTitleChanged();
                IsFileChanged = !IsSaved;
            }
        };

        FumenContentChanged += async (s, e) =>
        {
            await OnSimaiFileChangedAsync(CurrentMaidata);
            // 文本变更（已防抖 + 解析完成）-> 推送 Update
            await PushUpdateAsync();
        };
    }

    //------file operations

    public async Task<bool> AskSave()
    {
        if (!IsSaved)
        {
            var result = await MessageBox.ShowWindowDialogAsync(
                Langs.Msg_ChartNotSaved,
                Langs.Gui_Warning,
                ButtonEnum.YesNoCancel,
                Icon.Warning);

            switch (result)
            {
                case ButtonResult.Yes:
                    await SaveFile();
                    return false;
                case ButtonResult.No:
                    return false;
                default:
                    return true; // cancel
            }
        }
        return false;
    }

    public async Task ReloadFile()
    {
        if (string.IsNullOrEmpty(MaidataDir)) return;
        var maidataPath = Path.Combine(MaidataDir, "maidata.txt");
        if (!File.Exists(maidataPath)) return;
        await LoadChart(maidataPath);
    }

    public async Task NewChartFromDir(string directory)
    {
        SaveEditRecord();

        File.Create(Path.Combine(directory, "maidata.txt")).Dispose();
        var songTrackInfo = _trackReader.ReadTrack(directory);

        _editTimer.Reset();
        _editTimer.Start();

        MaidataDir = directory;
        SongTrackInfo = songTrackInfo;
        CurrentMaidata = MaidataFile.Parse("&title=Set Title\n&artist=Set Artist\n&first=0\n");
        SelectedDifficulty = 0;
        IsSaved = false;
        UpdateContext(MaidataDir);
        SetContent("");
        Enabled = true;
        await EditorLoad(MaidataDir);
    }

    [RelayCommand]
    public async Task NewFile()
    {
        if (await AskSave()) return;
        try
        {
            var file = await FileIOManager.DoOpenFilePickerAsync(FileIOManager.FileOpenerType.Track);
            if (file is null) return;
            var trackPath = file.TryGetLocalPath();
            if (trackPath is null) return;
            var fileInfo = new FileInfo(trackPath);
            var directory = fileInfo.Directory?.FullName;
            if (directory is null) return;
            if (File.Exists(Path.Combine(directory, "maidata.txt")))
            {
                await MessageBox.ShowWindowDialogAsync(
                    Langs.Msg_MaidataAlreadyExist,
                    Langs.Gui_Error,
                    ButtonEnum.Ok, Icon.Error);
                await LoadChart(Path.Combine(directory, "maidata.txt"));
                return;
            }
            await NewChartFromDir(directory);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    [RelayCommand]
    public async Task OpenFile()
    {
        if (await AskSave()) return;
        try
        {
            var file = await FileIOManager.DoOpenFilePickerAsync(FileIOManager.FileOpenerType.Maidata);
            if (file is null) return;
            var maidataPath = file.TryGetLocalPath();
            if (maidataPath is null) return;
            await LoadChart(maidataPath);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    public async Task OpenFile(string maidataPath)
    {
        if (await AskSave()) return;
        try
        {
            await LoadChart(maidataPath);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private async Task LoadChart(string maidataPath)
    {
        SaveEditRecord();

        var content = await File.ReadAllTextAsync(maidataPath);
        var maidataFile = MaidataFile.Parse(content);

        var fileInfo = new FileInfo(maidataPath);
        var directory = fileInfo.Directory?.FullName;
        if (directory is null) return;
        var songTrackInfo = _trackReader.ReadTrack(directory);

        MaidataDir = directory;
        SongTrackInfo = songTrackInfo;
        CurrentMaidata = maidataFile;
        UpdateContext(MaidataDir);
        SetContent(content);
        Enabled = true;

        LoadEditRecord();
        await EditorLoad(MaidataDir);
    }

    public void LoadEditRecord()
    {
        if (string.IsNullOrEmpty(MaidataDir)) return;

        var record = _editDb.GetRecord(MaidataDir);
        if (record is not null)
        {
            TrackTime = record.TrackTime;
            SelectedDifficulty = record.SelectedDifficulty;
            _editTimer.LoadAccumulated(record.TotalEditDuration);
        }
        else
        {
            _editTimer.Reset();
        }
        _editTimer.Start();
    }

    public void SaveEditRecord()
    {
        if (string.IsNullOrEmpty(MaidataDir)) return;

        _editTimer.Pause();
        var record = new ChartEditRecord
        {
            ChartPath = MaidataDir,
            SelectedDifficulty = SelectedDifficulty,
            TrackTime = TrackTime,
            TotalEditDuration = _editTimer.Elapsed
        };
        _editDb.UpsertRecord(record);
    }

    [RelayCommand]
    public async Task SaveFile()
    {
        if (CurrentMaidata.IsEmpty) return;

        var text = MaidataFile.Deparse(CurrentMaidata);
        var maidataPath = Path.Combine(MaidataDir, "maidata.txt");
        var tempPath = Path.Combine(MaidataDir, $".maidata.{Guid.NewGuid():N}.tmp");
        try
        {
            await using var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(text);

            File.Move(tempPath, maidataPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        MarkAsSaved();
        IsFileChanged = false;
    }

    //------disposal

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisposePlaybackAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to dispose playback resources: {ex}");
        }

        try
        {
            _trackReader.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to dispose audio resources: {ex}");
        }

        try
        {
            DisposeDiscordRpc();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to dispose Discord RPC: {ex}");
        }
    }
}
