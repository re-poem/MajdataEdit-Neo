using MajdataEdit_Neo.Assets.Langs;
using System.ComponentModel.DataAnnotations;

namespace MajdataEdit_Neo.Types.MajSetting;

public class MajEditSetting
{
    [Display(Name = nameof(Langs.Set_Language))]
    [SettingControl(SettingControlType.Selection,
        Values = new object[] {
            "zh-CN",
            "en-US"
        },
        Labels = new[] {
            "中文",
            "English"
        })]
    public string Language { get; set; } = "en-US";

    [Display(Name = nameof(Langs.Set_FontSize))]
    [SettingControl(SettingControlType.Numeric, Max = 100, Min = 1, Step = 1)]
    public float FontSize { get; set; } = 14f;

    [Display(Name = nameof(Langs.Set_AutoCheckUpdatesOnStartup))]
    [SettingControl(SettingControlType.Toggle)]
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;

    [Display(Name = nameof(Langs.Set_WaveAnimated))]
    [SettingControl(SettingControlType.Toggle)]
    public bool WaveAnimated { get; set; } = true;


    [Display(Name = nameof(Langs.Set_WordWrap))]
    [SettingControl(SettingControlType.Toggle)]
    public bool WordWrap { get; set; } = true;

    [Display(Name = nameof(Langs.Set_BackgroundImagePath))]
    public string BackgroundImagePath { get; set; } = "xxlb.png";
}
