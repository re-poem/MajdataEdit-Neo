using Newtonsoft.Json;
using System.Text;

namespace MajdataEdit_Neo.Types.MajWs;

/// <summary>
/// 共享 WS 协议的 JSON 序列化集中配置。两端必须使用同一组序列化选项，否则兼容性会出问题。
/// </summary>
internal static class WsJson
{
    private static readonly JsonSerializerSettings _settings = new()
    {
        // 不抛未知字段，方便日后只在一端追加新字段而不破坏对端旧版本。
        MissingMemberHandling = MissingMemberHandling.Ignore,
        // 写入时缺省值仍写出，便于排错（带宽影响可忽略，负载极小）。
        NullValueHandling = NullValueHandling.Include,
        // 不缩进，包更小。
        Formatting = Formatting.None,
    };

    public static string Serialize(object value) =>
        JsonConvert.SerializeObject(value, _settings);

    public static T? Deserialize<T>(string json) =>
        JsonConvert.DeserializeObject<T>(json, _settings);

    public static byte[] SerializeToUtf8(object value) =>
        Encoding.UTF8.GetBytes(Serialize(value));

    public static T? DeserializeFromUtf8<T>(byte[] bytes) =>
        Deserialize<T>(Encoding.UTF8.GetString(bytes));
}
