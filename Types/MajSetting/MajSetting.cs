using MajdataEdit_Neo.Assets.Langs;
using System.ComponentModel.DataAnnotations;

namespace MajdataEdit_Neo.Types.MajSetting;

public class MajSetting
{
    [Display(Name = nameof(Langs.Cat_EditSetting))]
    public MajEditSetting EditSetting { get; set; } = new();

    [Display(Name = nameof(Langs.Cat_ViewSetting))]
    public MajViewSetting ViewSetting { get; set; } = new();

    [Display(Name = nameof(Langs.Cat_VolumeSetting))]
    public MajVolumeSetting VolumeSetting { get; set; } = new();

    [SettingUnbrowsable]
    public MajWindowSetting WindowSetting { get; set; } = new();
}
