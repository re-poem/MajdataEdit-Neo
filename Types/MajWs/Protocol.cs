namespace MajdataEdit_Neo.Types.MajWs;

/// <summary>
/// 播放模式（线格式枚举）。成员顺序即数值，必须与 ViewX 端 PlaybackMode 一致（Normal, IncludeOp, Record, Preview）。
/// </summary>
internal enum PlaybackMode
{
    Normal,
    IncludeOp,
    Record,
    Preview
}

/// <summary>
/// 请求类型（线格式枚举）。数值必须与 ViewX 端 MajWsRequestType 完全一致。
/// 0=Setting, 1=Load, 2=Update, 3=Play, 4=Pause, 5=Stop, 6=State。
/// </summary>
internal enum MajWsRequestType
{
    Setting = 0,
    Load = 1,
    Update = 2,
    Play = 3,
    Pause = 4,
    Stop = 5,
    State = 6,
}

/// <summary>
/// 响应类型（线格式枚举）。数值必须与 ViewX 端 MajWsResponseType 完全一致。
/// </summary>
internal enum MajWsResponseType
{
    Error = 400,
    Ok = 200,
    PlayStarted = 201,
    PlayResumed = 202,
    Heartbeat = 203,
    PlayPaused = 204,
    PlayStopped = 205,
    LoadOk = 206
}

/// <summary>
/// 协议常量与端点信息。与 ViewX 端保持一致。
/// </summary>
internal static class WsProtocol
{
    /// <summary>线格式版本。字段顺序/类型一旦发布不可随意更改，只能追加；升级时先改这里。</summary>
    public const int ProtocolVersion = 3;

    public const int Port = 8083;
    public const string ServicePath = "/majdata";

    public static string ServerUrl => $"ws://127.0.0.1:{Port}{ServicePath}";
}
