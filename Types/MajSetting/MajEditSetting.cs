using System.ComponentModel.DataAnnotations;

namespace MajdataEdit_Neo.Types.MajSetting;

public class MajEditSetting
{
    [Display(Name = "yuyan")]
    public string Language { get; set; } = "en-US";
    public float FontSize { get; set; } = 14f;
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;
}
