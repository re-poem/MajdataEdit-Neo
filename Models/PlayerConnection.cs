using Avalonia.Threading;
using MajdataEdit_Neo.Base;
using MajdataEdit_Neo.Types.MajSetting;
using MajdataEdit_Neo.Types.MajWs;
using MajdataEdit_Neo.Utils;
using MajdataEdit_Neo.Types;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

namespace MajdataEdit_Neo.Models;

internal class PlayerConnection : IDisposable, IAsyncDisposable
{
    public bool IsConnected => _client?.IsAlive ?? false;

    public ViewSummary ViewSummary
    {
        get
        {
            lock (_stateSync)
                return _viewSummary;
        }
    }

    /// <summary>本地 ViewStatus 视图（State 在两端都是 ViewStatus 枚举）。</summary>
    public ViewStatus State
    {
        get
        {
            lock (_stateSync)
                return _viewSummary.State;
        }
    }

    private ViewSummary _viewSummary = new();

    public delegate void NotifyViewStateChangedEventHandler(object sender, MajWsResponseType e);
    public event NotifyViewStateChangedEventHandler? OnPlayStarted;
    public event NotifyViewStateChangedEventHandler? OnPlayStopped;
    public event EventHandler<ViewStatus>? OnViewStateChanged;

    public delegate void NotifyViewErrorEventHandler(object sender, string? error);
    public event NotifyViewErrorEventHandler? OnViewError;

    public event EventHandler? OnLoadRequired;
    public event EventHandler? OnStopRequired;
    public event EventHandler? OnLoadFinished;
    public event EventHandler? OnDisconnected;

    readonly object _stateSync = new();
    readonly object _connectionSync = new();
    readonly CancellationTokenSource _lifetimeCts = new();
    readonly SemaphoreSlim _connectGate = new(1, 1);
    readonly SemaphoreSlim _messageSignal = new(0);
    readonly SemaphoreSlim _stateChangedSignal = new(0, 1);
    readonly Task _listenerTask;
    bool _lastState;
    bool _disposed;
    WebSocket? _client;
    readonly ConcurrentQueue<MessageEventArgs> _playerMessages = new();
    public PlayerConnection()
    {
        _listenerTask = Task.Run(() => StartToListenWebSocket(_lifetimeCts.Token));
    }

    public async Task<bool> ConnectAsync(string? url = null)
    {
        url ??= WsProtocol.ServerUrl;
        if (IsConnected)
            return true;

        return await ConnectToPlayer(url);
    }

    private async Task<bool> ConnectToPlayer(string url)
    {
        await _connectGate.WaitAsync();
        try
        {
            if (IsConnected)
                return true;

            WebSocket client;
            lock (_connectionSync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_client is not null)
                    CloseClient(_client);

                client = new WebSocket(url)
                {
                    WaitTime = TimeSpan.FromSeconds(2)
                };
                client.OnClose += OnClose;
                client.OnOpen += OnOpen;
                client.OnMessage += OnMessage;
                client.OnError += OnError;
                _client = client;
            }

            await Task.Run(client.Connect);
            if (!client.IsAlive)
            {
                DiscardClient(client);
                return false;
            }

            Debug.WriteLine($"Connected to player: {url}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to connect to player: {ex}");
            var failedClient = _client;
            if (failedClient is not null && !failedClient.IsAlive)
                DiscardClient(failedClient);
            return false;
        }
        finally
        {
            _connectGate.Release();
        }
    }
    void OnOpen(object? sender, EventArgs args)
    {
        if (!ReferenceEquals(sender, _client))
            return;

        _lastState = true;
    }
    void OnClose(object? sender, CloseEventArgs args)
    {
        if (!ReferenceEquals(sender, _client))
            return;

        if (!_lastState)
            return;
        OnDisconnected?.Invoke(this, new EventArgs());
        _lastState = false;
        Signal(_stateChangedSignal);
    }
    void OnMessage(object? sender, MessageEventArgs args)
    {
        if (!ReferenceEquals(sender, _client))
            return;

        _playerMessages.Enqueue(args);
        Signal(_messageSignal);
    }
    void OnError(object? sender, ErrorEventArgs args)
    {
        Debug.WriteLine(args);
    }
    public async Task LoadAsync(string trackPath,
                                       string coverPath,
                                       string mvPath)
    {
        if (State == ViewStatus.Error) await StopAsync();

        if (State != ViewStatus.Loaded)
        {
            if (State is ViewStatus.Paused or ViewStatus.Playing)
            {
                OnStopRequired?.Invoke(this, new EventArgs());
            }

            //if busy, wait
            await WaitUntilNotBusyAsync();
        }
        var req = new MajWsRequest
        {
            Type = MajWsRequestType.Load,
            TrackPath = trackPath,
            ImagePath = coverPath,
            VideoPath = mvPath,
        };
        await SendAsync(req);
    }
    public async Task SettingAsync(MajViewSetting viewSetting, MajVolumeSetting volumeSetting)
    {
        var req = new MajWsRequest
        {
            Type = MajWsRequestType.Setting,
            ViewSetting = viewSetting,
            VolumeSetting = volumeSetting,
        };
        await SendAsync(req);
    }

    /// <summary>
    /// 把已解析的谱面推给播放器：
    /// 谱面文本、元数据（Title / Artist / Level / Designer / Offset / ClockCount）
    /// 全部直接走单条 WS 文本帧传输，不再经共享内存中转。
    /// </summary>
    public async Task UpdateAsync(MaidataFile file, int selectedDifficulty, string chartText, string level, string designer)
    {
        var clockCount = 0;
        foreach (var cmd in file.Commands)
        {
            if (cmd.Key == "clock_count")
            {
                int.TryParse(cmd.Value, out clockCount);
                break;
            }
        }

        var req = new MajWsRequest
        {
            Type = MajWsRequestType.Update,
            ChartText = chartText ?? string.Empty,
            SelectedDifficulty = selectedDifficulty,
            Title = file.Title,
            Artist = file.Artist,
            Level = level ?? string.Empty,
            Designer = designer ?? string.Empty,
            Offset = file.Offset,
            ClockCount = clockCount,
        };
        await SendAsync(req);
    }

    /// <summary>
    /// 瘦身后的 Play：图数据已由 Update 提供，这里只带播放参数。
    /// </summary>
    public async Task PlayAsync(PlaybackMode mode, double startAt, float speed, string? maidataPath = null)
    {
        if (State == ViewStatus.Error) await StopAsync();

        if (State == ViewStatus.Idle)
        {
            OnLoadRequired?.Invoke(this, new EventArgs());

            //if busy, wait
            await WaitUntilNotBusyAsync();
        }

        var req = new MajWsRequest
        {
            Type = MajWsRequestType.Play,
            PlayMode = mode,
            StartAt = startAt,
            Speed = speed,
            MaidataPath = maidataPath,
        };
        await SendAsync(req);
    }
    public async Task PauseAsync()
    {
        var req = new MajWsRequest { Type = MajWsRequestType.Pause };
        await SendAsync(req);
    }
    public async Task StopAsync()
    {
        var req = new MajWsRequest { Type = MajWsRequestType.Stop };
        await SendAsync(req);
    }
    async Task SendAsync(MajWsRequest req)
    {
        var client = _client;
        if (client is null || !client.IsAlive)
            throw new PlayerNotConnectedException();

        // 文本帧（JSON 字符串）；websocket-sharp 在传入 string 时发送文本帧。
        var json = WsJson.Serialize(req);
        await Task.Run(() => client.Send(json));
        Debug.WriteLine($"Player request sent: {req.Type}");
    }
    private async Task WaitUntilNotBusyAsync()
    {
        while (State == ViewStatus.Busy)
            await _stateChangedSignal.WaitAsync(_lifetimeCts.Token);
    }

    async Task StartToListenWebSocket(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _messageSignal.WaitAsync(cancellationToken);
                while (_playerMessages.TryDequeue(out var args))
                {
                    try
                    {
                        await ProcessMessageAsync(args);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to process player message: {ex}");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Player message listener failed: {ex}");
        }
    }

    private async Task ProcessMessageAsync(MessageEventArgs args)
    {
        // 文本帧为 JSON；二进制帧按 UTF-8 解码为 JSON（兼容 ViewX 误发二进制的情况）
        var json = args.IsBinary ? Encoding.UTF8.GetString(args.RawData) : args.Data;
        var resp = WsJson.Deserialize<MajWsResponse>(json);
        if (resp is null)
            return;
        switch (resp.ResponseType)
        {
            case MajWsResponseType.PlayPaused:
            case MajWsResponseType.Heartbeat:
            case MajWsResponseType.Ok:
                if (resp.Summary is not null) UpdateViewSummary(resp.Summary);
                break;
            case MajWsResponseType.LoadOk:
                if (resp.Summary is not null) UpdateViewSummary(resp.Summary);
                OnLoadFinished?.Invoke(this, EventArgs.Empty);
                break;
            case MajWsResponseType.PlayResumed:
            case MajWsResponseType.PlayStarted:
                if (resp.Summary is not null) UpdateViewSummary(resp.Summary);
                OnPlayStarted?.Invoke(this, resp.ResponseType);
                break;
            case MajWsResponseType.PlayStopped:
                if (resp.Summary is not null) UpdateViewSummary(resp.Summary);
                OnPlayStopped?.Invoke(this, resp.ResponseType);
                break;
            case MajWsResponseType.Error:
                OnViewStateChanged?.Invoke(this, State);
                OnViewError?.Invoke(this, resp.Error);
                break;
        }
        await Task.CompletedTask;
    }

    private void UpdateViewSummary(ViewSummary summary)
    {
        ViewStatus oldState;
        lock (_stateSync)
        {
            oldState = _viewSummary.State;
            _viewSummary = summary;
        }

        Signal(_stateChangedSignal);
        if (oldState != summary.State)
            OnViewStateChanged?.Invoke(this, summary.State);
    }

    private static void Signal(SemaphoreSlim semaphore)
    {
        try
        {
            semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private void CloseClient(WebSocket client)
    {
        client.OnClose -= OnClose;
        client.OnOpen -= OnOpen;
        client.OnMessage -= OnMessage;
        client.OnError -= OnError;
        try
        {
            client.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to close player connection: {ex}");
        }
    }

    private void DiscardClient(WebSocket client)
    {
        lock (_connectionSync)
        {
            if (ReferenceEquals(_client, client))
                _client = null;
        }
        CloseClient(client);
    }

    public void Dispose()
    {
        lock (_connectionSync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _lifetimeCts.Cancel();
            if (_client is not null)
            {
                CloseClient(_client);
                _client = null;
            }
        }

        _lifetimeCts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        try
        {
            await _listenerTask;
        }
        catch (OperationCanceledException)
        {
        }
        _messageSignal.Dispose();
        _stateChangedSignal.Dispose();
        _connectGate.Dispose();
    }
}
internal class PlayerNotConnectedException : Exception
{
    public PlayerNotConnectedException() : base() { }
}
