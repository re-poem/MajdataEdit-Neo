using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_Neo.Types.MajSetting;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace MajdataEdit_Neo.ViewModels;

partial class SettingsViewModel : ViewModelBase
{
    public MajEditSetting EditSetting 
    { 
        get => _editSetting;
        set 
        {
            if (value != null)
            {
                _editSetting = value;
                EditSettingItems = GenerateItems(EditSetting);
            }
        } 
    }
    public MajViewSetting ViewSetting
    {
        get => _viewSetting;
        set
        {
            if (value != null)
            {
                _viewSetting = value;
                ViewSettingItems = GenerateItems(ViewSetting);
            }
        }
    }

    [ObservableProperty]
    List<SettingItem> editSettingItems = new();
    [ObservableProperty]
    List<SettingItem> viewSettingItems = new();

    MajEditSetting _editSetting;
    MajViewSetting _viewSetting;

    private List<SettingItem> GenerateItems(object obj)
    {
        return obj.GetType()
            .GetProperties()
            .Select(p => new SettingItem(obj, p))
            .ToList();
    }
}

public partial class SettingItem : ObservableObject
{
    private readonly object _owner;
    private readonly PropertyInfo _prop;

    public string Name { get; }
    public string DisplayName { get; }
    public Type PropertyType { get; }

    public SettingItem(object owner, PropertyInfo prop)
    {
        _owner = owner;
        _prop = prop;
        Name = prop.Name;
        PropertyType = prop.PropertyType;

        var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
        DisplayName = displayAttr?.Name ?? prop.Name; // 如果没写特性，就用原始属性名
    }

    public object? Value
    {
        get => _prop.GetValue(_owner);
        set
        {
            try
            {
                // 处理数值转换，NumericUpDown 可能会传回 decimal 或 double
                var convertedValue = Convert.ChangeType(value, _prop.PropertyType);
                _prop.SetValue(_owner, convertedValue);
                OnPropertyChanged();
            }
            catch { /* 转换失败处理 */ }
        }
    }

    // 专门为 Enum 提供的选项列表
    public IEnumerable<object> EnumOptions => PropertyType.IsEnum
        ? Enum.GetValues(PropertyType).Cast<object>()
        : Array.Empty<object>();
}