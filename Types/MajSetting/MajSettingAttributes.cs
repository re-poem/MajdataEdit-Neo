using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MajdataEdit_Neo.Types.MajSetting;

//Supports Attrs: Display, Range, SettingsControl

public enum SettingControlType { Default, Slider, Numeric, Toggle, Selection }

[AttributeUsage(AttributeTargets.Property)]
public class SettingControlAttribute(SettingControlType type) : Attribute
{
    public SettingControlType Type { get; } = type;
    public double Step { get; set; } = 1.0; // only for Numeric/Slider
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public object[]? Values { get; set; } //only for Selection
    public string[]? Labels { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingUnbrowsableAttribute : Attribute;