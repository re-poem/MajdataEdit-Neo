using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MajdataEdit_Neo.Assets.Langs;
using MajdataEdit_Neo.Base;
using MajdataEdit_Neo.Models;
using MajdataEdit_Neo.Types;
using MajdataEdit_Neo.Types.MajSetting;
using MajdataEdit_Neo.Types.MajWs;
using MajdataEdit_Neo.Utils;
using MajSimai;
using MsBox.Avalonia.Enums;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace MajdataEdit_Neo.ViewModels;

/// <summary>
/// 播放器连接、播放控制、光标跟随
/// </summary>
public partial class MainWindowViewModel
{
    private MemoryMappedFile mmfAudioTime = null!;
    private MemoryMappedViewAccessor mmvAudioTime = null!;

    [ObservableProperty]
    private float _playbackSpeed = 1f;

    [ObservableProperty]
    private double _trackTime = 0d;

    partial void OnTrackTimeChanged(double value) => OnPropertyChanged(nameof(DisplayTime));

    [ObservableProperty]
    private float _trackZoomLevel = 4f;

    [ObservableProperty]
    private double _caretTime = 0d;

    [ObservableProperty]
    private bool _isFollowCursor;

    [ObservableProperty]
    private ViewStatus _currentViewState = ViewStatus.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLineComboText))]
    private int _currentCombo = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLineComboText))]
    private int _caretLine = 1;

    internal readonly PlayerConnection _playerConnection = new();
    internal double _playStartTime = 0d;
    internal bool _isBackToStartOnPlayStop = false;
    internal bool _isStopping = false;
    internal bool _isLastPlayIncludeOp = false;
    private bool _updateDirty; // 播放中收到的文本变更标记，停播/播放前补发
    private readonly Lock _playbackTrackingLock = new();
    private CancellationTokenSource? _playbackTrackingCts;
    private Task _playbackTrackingTask = Task.CompletedTask;
    private SimaiChart? _followChart;
    private int _followTimingIndex = -1;
    private int _lastReportedFollowTimingIndex = -1;
    private double _lastFollowChartTime = double.NegativeInfinity;
    private bool _disposed;

    public event Action<Point>? RequestSeekToDocPos;

    //------initialization

    private void InitializePlayback()
    {
        _playerConnection.OnPlayStarted += OnPlayStarted;
        _playerConnection.OnPlayStopped += OnPlayStopped;
        _playerConnection.OnLoadRequired += OnLoadRequired;
        _playerConnection.OnStopRequired += OnStopRequired;
        _playerConnection.OnDisconnected += OnDisconnected;
        _playerConnection.OnViewError += OnViewError;
        _playerConnection.OnViewStateChanged += OnViewStateChanged;

        Directory.CreateDirectory(MajEnv.MajdataViewPersistentDataPath);
        var mmfAudioTimeFileStream = new FileStream(
            MajEnv.MmfAudioTimePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite
        );
        if (mmfAudioTimeFileStream.Length < sizeof(float))
            mmfAudioTimeFileStream.SetLength(sizeof(float));
        mmfAudioTime = MemoryMappedFile.CreateFromFile(
            mmfAudioTimeFileStream,
            null,
            sizeof(float),
            MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None,
            false
        );
        mmvAudioTime = mmfAudioTime.CreateViewAccessor();
    }

    //------derived properties

    public bool IsConnected => _playerConnection.IsConnected;

    public string DisplayLineComboText => $"L {CaretLine}  Cb {CurrentCombo}";

    public string DisplayTime
    {
        get
        {
            var minute = (int)TrackTime / 60;
            double second = (int)(TrackTime - 60 * minute);
            return string.Format("{0}:{1:00}", minute, second);
        }
    }

    //------player connection

    public async Task<bool> ConnectToPlayerAsync()
    {
        if (!await _playerConnection.ConnectAsync())
        {
            OnPropertyChanged(nameof(IsConnected));
            return false;
        }
        OnPropertyChanged(nameof(IsConnected));
        return true;
    }

    public async Task<bool> CheckPlayerConnectionAndReconnect(bool showMessageBox = false)
    {
        if (!_playerConnection.IsConnected)
        {
            if (!await _playerConnection.ConnectAsync())
            {
                OnPropertyChanged(nameof(IsConnected));
                return false;
            }
        }
        OnPropertyChanged(nameof(IsConnected));
        return true;
    }

    //------playback control

    public void SlideZoomLevel(float delta)
    {
        var level = TrackZoomLevel + delta;
        if (level <= 0.1f) level = 0.1f;
        if (level > 10f) level = 10f;
        TrackZoomLevel = level;
    }

    public Point SlideTrackTime(double delta, TrackInfo? songTrackInfo, SimaiChart? chartData, float offset)
    {
        if (songTrackInfo is null) return new Point();
        var time = TrackTime - delta * 0.2 * TrackZoomLevel;
        if (time < 0) time = 0;
        else if (time > songTrackInfo.Length) time = songTrackInfo.Length;
        if (_playerConnection.State is ViewStatus.Playing)
        {
            Pause();
        }
        TrackTime = time;
        mmvAudioTime.Write(0, (float)time);

        if (chartData is null) return new Point();
        var timings = chartData.CommaTimings;
        if (timings.Length == 0) return new Point();
        var chartTime = time - offset;
        var index = FindTimingIndexAtOrBefore(timings, chartTime);
        if (index + 1 < timings.Length &&
            (index < 0 ||
             Math.Abs(timings[index + 1].Timing - chartTime) <
             Math.Abs(timings[index].Timing - chartTime)))
        {
            index++;
        }
        if (index < 0) index = 0;
        var nearestNote = timings[index];

        return new Point(nearestNote.RawTextPositionX, nearestNote.RawTextPositionY - 1);
    }

    [RelayCommand]
    public void IncreasePlaybackSpeed()
    {
        PlaybackSpeed += 0.1f;
    }

    [RelayCommand]
    public void DecreasePlaybackSpeed()
    {
        var speed = PlaybackSpeed - 0.1f;
        if (speed < 0.1f) speed = 0.1f;
        PlaybackSpeed = speed;
    }

    public void SetCaretPosition(int rawPosition, int line, bool setTrackTime)
    {
        CaretLine = line;

        var chartData = CurrentChartData;

        var commaTs = chartData.CommaTimings;
        var nearestTiming = commaTs.Length > 0 ? commaTs[0] : default;
        foreach (var commaT in commaTs)
        {
            if (commaT.RawTextPosition >= rawPosition)
            {
                nearestTiming = commaT;
                break;
            }
        }
        CaretTime = nearestTiming?.Timing ?? 0;

        var noteTs = chartData.NoteTimings;
        var currentCombo = 0;
        foreach (var noteT in noteTs)
        {
            if (noteT.RawTextPosition >= rawPosition) break;
            foreach (var note in noteT.Notes)
                if (note.Type is SimaiNoteType.Slide && !note.IsSlideNoHead) currentCombo += 2;
                else currentCombo++;
        }
        CurrentCombo = currentCombo;

        if (setTrackTime)
        {
            TrackTime = CaretTime + Offset;
            mmvAudioTime.Write(0, (float)TrackTime);
        }
    }

    public async Task EditorLoad(string maidataDir)
    {
        try
        {
            var useOgg = System.IO.File.Exists(maidataDir + "/track.ogg");
            var trackPath = maidataDir + "/track" + (useOgg ? ".ogg" : ".mp3");

            var bgPath = maidataDir + "/bg.jpg";
            if (!System.IO.File.Exists(bgPath)) bgPath = maidataDir + "/bg.png";
            if (!System.IO.File.Exists(bgPath)) bgPath = "";

            var pvPath = maidataDir + "/pv.mp4";
            if (!System.IO.File.Exists(pvPath)) pvPath = maidataDir + "/bg.mp4";
            if (!System.IO.File.Exists(pvPath)) pvPath = "";

            if (!await CheckPlayerConnectionAndReconnect())
            {
                return;
            }
            await _playerConnection.LoadAsync(trackPath, bgPath, pvPath);
            await _playerConnection.SettingAsync(Settings.ViewSetting, Settings.VolumeSetting);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load editor: {ex}");
        }
    }

    public async Task PushUpdateAsync(bool force = false)
    {
        var simai = CurrentSimaiFile;
        if (simai is null || !_playerConnection.IsConnected)
            return;

        if (!force && _playerConnection.State is ViewStatus.Playing or ViewStatus.Paused)
        {
            _updateDirty = true;
            return;
        }

        _updateDirty = false;
        await _playerConnection.UpdateAsync(simai, CurrentChartData, SelectedDifficulty);
    }

    //------commands

    [RelayCommand]
    public void PlayRecord()
    {
        if (CurrentSimaiFile == null) return;
        _ = PlayRecord(Settings, MaidataDir);
    }

    public async Task PlayRecord(MajSetting settings, string maidataDir)
    {
        if (!await CheckPlayerConnectionAndReconnect(true))
        {
            return;
        }

        _playStartTime = TrackTime;
        if (_updateDirty) await PushUpdateAsync(force: true);
        await _playerConnection.SettingAsync(settings.ViewSetting, settings.VolumeSetting);
        await _playerConnection.PlayAsync(PlaybackMode.Record, _playStartTime, PlaybackSpeed, maidataDir);
        _isLastPlayIncludeOp = false;
    }

    [RelayCommand]
    public void PlayIncludeOp()
    {
        if (CurrentSimaiFile == null) return;
        _ = PlayIncludeOp(Settings);
    }

    public async Task PlayIncludeOp(MajSetting settings)
    {
        if (!await CheckPlayerConnectionAndReconnect(true))
        {
            return;
        }
        _playStartTime = TrackTime;
        if (_updateDirty) await PushUpdateAsync(force: true);
        await _playerConnection.SettingAsync(settings.ViewSetting, settings.VolumeSetting);
        await _playerConnection.PlayAsync(PlaybackMode.IncludeOp, _playStartTime, PlaybackSpeed);
        _isLastPlayIncludeOp = true;
    }

    [RelayCommand]
    public void PlayStop()
    {
        if (CurrentSimaiFile == null) return;
        _ = PlayStop(Settings);
    }

    public async Task PlayStop(MajSetting settings)
    {
        if (!await CheckPlayerConnectionAndReconnect(true))
        {
            TrackTime = _playStartTime;
            return;
        }

        switch (_playerConnection.State)
        {
            case ViewStatus.Playing:
                _isBackToStartOnPlayStop = true;
                await _playerConnection.StopAsync();
                return;
        }
        _playStartTime = TrackTime;
        if (_updateDirty) await PushUpdateAsync(force: true);
        await _playerConnection.SettingAsync(settings.ViewSetting, settings.VolumeSetting);
        await _playerConnection.PlayAsync(PlaybackMode.Normal, _playStartTime, PlaybackSpeed);
        _isLastPlayIncludeOp = false;
    }

    [RelayCommand]
    public void PlayPause()
    {
        if (CurrentSimaiFile == null) return;
        _ = PlayPause(Settings);
    }

    public async Task PlayPause(MajSetting settings)
    {
        if (!await CheckPlayerConnectionAndReconnect(true))
        {
            return;
        }

        switch (_playerConnection.State)
        {
            case ViewStatus.Playing:
                await _playerConnection.PauseAsync();
                return;
        }
        _playStartTime = TrackTime;
        if (_updateDirty) await PushUpdateAsync(force: true);
        await _playerConnection.SettingAsync(settings.ViewSetting, settings.VolumeSetting);
        await _playerConnection.PlayAsync(PlaybackMode.Normal, _playStartTime, PlaybackSpeed);
        _isLastPlayIncludeOp = false;
    }

    [RelayCommand]
    public void Stop() => Stop(true);

    public async void Stop(bool toStart = true)
    {
        try
        {
            _isStopping = true;
            _isBackToStartOnPlayStop = toStart;

            if (!await CheckPlayerConnectionAndReconnect())
            {
                if (toStart)
                    TrackTime = _playStartTime;
                return;
            }

            await _playerConnection.StopAsync();
        }
        finally
        {
            _isStopping = false;
        }
    }

    public async void Pause()
    {
        if (!await CheckPlayerConnectionAndReconnect(true))
        {
            return;
        }

        switch (_playerConnection.State)
        {
            case ViewStatus.Playing:
                await _playerConnection.PauseAsync();
                return;
            case ViewStatus.Paused:
                return;
        }
    }

    //------player events

    private async void OnPlayStarted(object sender, MajWsResponseType e)
    {
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        Task trackingTask;
        lock (_playbackTrackingLock)
        {
            var previousCts = _playbackTrackingCts;
            _playbackTrackingCts = cts;
            previousCts?.Cancel();
            previousCts?.Dispose();

            Dispatcher.UIThread.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                CurrentViewState = ViewStatus.Playing;
                ResetFollowCursorIndex();
            });

            trackingTask = TrackPlaybackAsync(cancellationToken);
            _playbackTrackingTask = trackingTask;
        }

        WeakReferenceMessenger.Default.Send(new FocusEditorMsg());
        MajEnv.ActivateProcessWindow(Process.GetProcessesByName("MajdataViewX").FirstOrDefault());

        try
        {
            await trackingTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Start Read Play Time MMV Err:{ex}");
        }
    }

    private async void OnPlayStopped(object sender, MajWsResponseType e)
    {
        var trackingTask = CancelPlaybackTracking();
        try
        {
            await trackingTask;
        }
        catch (OperationCanceledException)
        {
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CurrentViewState = ViewStatus.Idle;
            if (_isBackToStartOnPlayStop) TrackTime = _playStartTime;
        });

        // 播放中堆积的文本变更：停播后补发最新一份
        if (_updateDirty) await PushUpdateAsync(force: true);
    }

    private async void OnLoadRequired(object? sender, EventArgs e)
    {
        await EditorLoad(MaidataDir);
    }

    private void OnStopRequired(object? sender, EventArgs e)
    {
        Stop();
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        CancelPlaybackTracking();
        Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(IsConnected)));
    }

    private void OnViewStateChanged(object? sender, ViewStatus e)
    {
        Dispatcher.UIThread.Post(() => CurrentViewState = e);
    }
    private async void OnViewError(object? sender, string? error)
    {
        ShowStatusMessage(error ?? string.Empty);
        await Task.Delay(3000);
        ResetStatusMessage();
    }

    //------playback tracking

    private async Task TrackPlaybackAsync(CancellationToken cancellationToken)
    {
        while (_playerConnection.State == ViewStatus.Playing &&
               _playerConnection.IsConnected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trackTime = mmvAudioTime.ReadSingle(0);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                TrackTime = trackTime;
                if (!IsFollowCursor)
                    return;

                var point = GetFollowCursorPoint(trackTime);
                if (point is not null)
                    RequestSeekToDocPos?.Invoke(point.Value);
            });

            await Task.Delay(16, cancellationToken);
        }
    }

    private Point? GetFollowCursorPoint(double trackTime)
    {
        var chart = CurrentChartData;
        var timings = chart.CommaTimings;
        if (timings.Length == 0)
            return null;

        var chartTime = trackTime - Offset;
        if (!ReferenceEquals(chart, _followChart) ||
            chartTime < _lastFollowChartTime ||
            chartTime - _lastFollowChartTime > 0.5 ||
            _followTimingIndex >= timings.Length)
        {
            if (!ReferenceEquals(chart, _followChart))
                _lastReportedFollowTimingIndex = -1;
            _followChart = chart;
            _followTimingIndex = FindTimingIndexAtOrBefore(timings, chartTime);
        }
        else
        {
            while (_followTimingIndex + 1 < timings.Length &&
                   timings[_followTimingIndex + 1].Timing <= chartTime)
            {
                _followTimingIndex++;
            }
        }

        _lastFollowChartTime = chartTime;
        if (_followTimingIndex < 0 ||
            _followTimingIndex == _lastReportedFollowTimingIndex)
            return null;

        _lastReportedFollowTimingIndex = _followTimingIndex;
        var timing = timings[_followTimingIndex];
        return new Point(timing.RawTextPositionX, timing.RawTextPositionY - 1);
    }

    private void ResetFollowCursorIndex()
    {
        _followChart = null;
        _followTimingIndex = -1;
        _lastReportedFollowTimingIndex = -1;
        _lastFollowChartTime = double.NegativeInfinity;
    }

    private Task CancelPlaybackTracking()
    {
        lock (_playbackTrackingLock)
        {
            var cts = _playbackTrackingCts;
            _playbackTrackingCts = null;
            cts?.Cancel();
            cts?.Dispose();
            return _playbackTrackingTask;
        }
    }

    private static int FindTimingIndexAtOrBefore(ReadOnlySpan<SimaiTimingPoint> timings, double chartTime)
    {
        var low = 0;
        var high = timings.Length - 1;
        var result = -1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            if (timings[middle].Timing <= chartTime)
            {
                result = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return result;
    }

    private async ValueTask DisposePlaybackAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        var trackingTask = CancelPlaybackTracking();
        try
        {
            await trackingTask;
        }
        catch (OperationCanceledException)
        {
        }
        _playerConnection.OnPlayStarted -= OnPlayStarted;
        _playerConnection.OnPlayStopped -= OnPlayStopped;
        _playerConnection.OnLoadRequired -= OnLoadRequired;
        _playerConnection.OnStopRequired -= OnStopRequired;
        _playerConnection.OnDisconnected -= OnDisconnected;
        _playerConnection.OnViewStateChanged -= OnViewStateChanged;
        try
        {
            await _playerConnection.DisposeAsync();
        }
        finally
        {
            mmvAudioTime.Dispose();
            mmfAudioTime.Dispose();
        }
    }
}
