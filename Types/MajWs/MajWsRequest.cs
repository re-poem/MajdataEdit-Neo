using MajdataEdit_Neo.Types.MajSetting;
using Newtonsoft.Json;

namespace MajdataEdit_Neo.Types.MajWs;

/// <summary>
/// 请求信封（线格式：JSON 文本帧，Newtonsoft.Json 序列化）。
/// 所有控制命令、元数据与谱面文本都通过单条 WS 消息传输，不再走 MMF 共享内存，
/// 所以采用扁平结构 + type 判别字段，避免多态序列化带来的复杂度。
///
/// 字段命名（即 JSON 键）属于线格式契约，必须与 ViewX 端 MajWsRequest 完全一致；
/// 增减字段时请同时改两端，并通过 <see cref="WsProtocol.ProtocolVersion"/> 升版本。
/// </summary>
internal class MajWsRequest
{
    /// <summary>请求类型判别字段。0=Setting, 1=Load, 2=Update, 3=Play, 4=Pause, 5=Stop, 6=State。</summary>
    [JsonProperty("type")]
    public MajWsRequestType Type { get; set; }

    // === Setting (type=0) ===
    [JsonProperty("viewSetting", NullValueHandling = NullValueHandling.Ignore)]
    public MajViewSetting? ViewSetting { get; set; }
    [JsonProperty("volumeSetting", NullValueHandling = NullValueHandling.Ignore)]
    public MajVolumeSetting? VolumeSetting { get; set; }

    // === Load (type=1)：媒体文件路径不走线格式（本地文件由双方约定绝对路径） ===
    [JsonProperty("trackPath", NullValueHandling = NullValueHandling.Ignore)]
    public string? TrackPath { get; set; }
    [JsonProperty("imagePath", NullValueHandling = NullValueHandling.Ignore)]
    public string? ImagePath { get; set; }
    [JsonProperty("videoPath", NullValueHandling = NullValueHandling.Ignore)]
    public string? VideoPath { get; set; }

    // === Update (type=2) ===
    /// <summary>当前难度谱面文本（UTF-8）。直接走 WS 文本帧传输，不再经共享内存。</summary>
    [JsonProperty("chartText", NullValueHandling = NullValueHandling.Ignore)]
    public string? ChartText { get; set; }
    [JsonProperty("selectedDifficulty")]
    public int SelectedDifficulty { get; set; }
    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }
    [JsonProperty("artist", NullValueHandling = NullValueHandling.Ignore)]
    public string? Artist { get; set; }
    [JsonProperty("level", NullValueHandling = NullValueHandling.Ignore)]
    public string? Level { get; set; }
    [JsonProperty("designer", NullValueHandling = NullValueHandling.Ignore)]
    public string? Designer { get; set; }
    [JsonProperty("offset")]
    public float Offset { get; set; }
    [JsonProperty("clockCount")]
    public int ClockCount { get; set; }

    // === Play (type=3) ===
    [JsonProperty("playMode")]
    public PlaybackMode PlayMode { get; set; }
    [JsonProperty("startAt")]
    public double StartAt { get; set; }
    [JsonProperty("speed")]
    public float Speed { get; set; } = 1f;
    [JsonProperty("maidataPath", NullValueHandling = NullValueHandling.Ignore)]
    public string? MaidataPath { get; set; }

    // === Pause (type=4), Stop (type=5), State (type=6)：无载荷 ===
}