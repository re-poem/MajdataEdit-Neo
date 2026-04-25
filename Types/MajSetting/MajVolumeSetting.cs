using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MajdataEdit_Neo.Types.MajSetting;

public class MajVolumeSetting
{
    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    public float Answer { get; set; } = 0.8f;

    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    public float Break { get; set; } = 0.7f;

    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    public float Slide { get; set; } = 0.3f;

    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    public float Tap { get; set; } = 0.45f;

    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    public float Touch { get; set; } = 0.7f;

    [SettingControl(SettingControlType.Slider, Max = 1, Min = 0, Step = 0.01)]
    public float Track { get; set; } = 0.9f;
}
