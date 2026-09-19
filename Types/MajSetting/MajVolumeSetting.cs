using MajdataEdit_Neo.Assets.Langs;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace MajdataEdit_Neo.Types.MajSetting;

// 线格式契约：字段命名 / 默认值必须与 ViewX 端 MajVolumeSetting 完全一致。
// JSON 键由 [JsonProperty] 锁定；其余属性仅为 Edit 端 UI 元数据，不参与线格式。
public partial class MajVolumeSetting
{
    [Display(Name = nameof(SampleType.Track))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("track")]
    public float Track { get; set; } = 0.9f;
    [Display(Name = nameof(SampleType.Answer))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("answer")]
    public float Answer { get; set; } = 0.9f;
    [Display(Name = nameof(SampleType.Tap))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("tap")]
    public float Tap { get; set; } = 0.9f;
    [Display(Name = nameof(SampleType.Slide))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("slide")]
    public float Slide { get; set; } = 0.9f;
    [Display(Name = nameof(SampleType.Break))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("break")]
    public float Break { get; set; } = 0.9f;
    [Display(Name = nameof(SampleType.BreakSlide))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("breakSlide")]
    public float BreakSlide { get; set; } = 0.9f;
    [Display(Name = nameof(SampleType.Ex))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("ex")]
    public float Ex { get; set; } = 0.9f;
    [Display(Name = nameof(SampleType.Touch))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("touch")]
    public float Touch { get; set; } = 0.9f;
    [Display(Name = nameof(SampleType.Hanabi))]
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    [JsonProperty("hanabi")]
    public float Hanabi { get; set; } = 0.9f;
}
public enum SampleType
{
    Track,
    Answer,
    Tap,
    Slide,
    Break,
    BreakSlide,
    Ex,
    Touch,
    Hanabi
}
