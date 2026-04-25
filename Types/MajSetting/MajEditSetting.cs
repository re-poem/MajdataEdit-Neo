using MajdataEdit_Neo.Assets.Langs;
using System.ComponentModel.DataAnnotations;

namespace MajdataEdit_Neo.Types.MajSetting;

public class MajEditSetting
{
    [Display(Name = nameof(Langs.Set_Language))]
    [SettingControl(SettingControlType.Selection,
        Values = new object[] { "zh-CN", "en-US" },
        Labels = new[] { "中文", "English" })]
    public string Language { get; set; } = "en-US";

    [SettingControl(SettingControlType.Numeric, Max = 100, Min = 0, Step = 0.1)]
    public float FontSize { get; set; } = 14f;

    [SettingControl(SettingControlType.Toggle)]
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;
}
