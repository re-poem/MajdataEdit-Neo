using Avalonia.Markup.Xaml.MarkupExtensions;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_Neo.Assets.Langs;
using MajdataEdit_Neo.Types.MajSetting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MajdataEdit_Neo.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<SettingCategory> categories = new();

    public void LoadSettings(MajSetting settingInstance)
    {
        foreach (var categoryProp in typeof(MajSetting).GetProperties())
        {
            var subSection = categoryProp.GetValue(settingInstance);
            if (subSection == null) continue;

            var display = categoryProp.GetCustomAttribute<DisplayAttribute>();
            var category = new SettingCategory(categoryProp)
            {
                Items = subSection.GetType()
                    .GetProperties()
                    .Select(p => new SettingItem(subSection, p))
                    .ToList()
            };
            Categories.Add(category);
        }
    }
}

public partial class SettingCategory : ObservableObject
{
    private readonly string _titleKey;
    private readonly DisplayAttribute? _display;

    public SettingCategory(PropertyInfo prop)
    {
        _display = prop.GetCustomAttribute<DisplayAttribute>();
        _titleKey = prop.Name;
    }

    public string Title => _display?.Name ?? _titleKey;
    public List<SettingItem> Items { get; set; } = new();
}


public partial class SettingItem : ObservableObject
{
    private readonly object _owner;
    private readonly PropertyInfo _prop;

    private readonly DisplayAttribute? _display;
    private readonly SettingControlAttribute? _ctrl;

    public string DisplayName => _display?.Name ?? _prop.Name;

    public SettingControlType ControlType => _ctrl?.Type ?? SettingControlType.Default;

    public double Min => _ctrl?.Min ?? 0; // only for Numeric/Slider
    public double Max => _ctrl?.Max ?? 100;
    public double Step => _ctrl?.Step ?? 1.0;

    public List<SelectionValue> SelectionValues { get; } = new(); //only for Selection


    [ObservableProperty]
    public partial SelectionValue? SelectedValue { get; set; }
    partial void OnSelectedValueChanged(SelectionValue? value)
    {
        if (value != null && !Equals(value.Value, Value))
            Value = value.Value;
    }

    public SettingItem(object owner, PropertyInfo prop)
    {
        _owner = owner;
        _prop = prop;
        _display = prop.GetCustomAttribute<DisplayAttribute>();
        _ctrl = prop.GetCustomAttribute<SettingControlAttribute>();

        InitializeSelection();
    }

    private void InitializeSelection()
    {
        // only some values
        if (_ctrl?.Values != null)
        {
            for (int i = 0; i < _ctrl.Values.Length; i++)
            {
                var labelKey = (_ctrl.Labels != null && i < _ctrl.Labels.Length) ? _ctrl.Labels[i] : _ctrl.Values[i].ToString()!;
                SelectionValues.Add(new SelectionValue(_ctrl.Values[i], labelKey));
            }
        }
        // or all
        else if (_prop.PropertyType.IsEnum)
        {
            foreach (var val in Enum.GetValues(_prop.PropertyType))
            {
                SelectionValues.Add(new SelectionValue(val, val.ToString()!));
            }
        }

        if (SelectionValues.Count != 0)
            SelectedValue = SelectionValues.FirstOrDefault(x => Equals(x.Value, Value));
    }

    public object? Value
    {
        get => _prop.GetValue(_owner);
        set
        {
            if (value == null) return;
            try
            {
                var targetType = _prop.PropertyType;
                object? converted;

                if (targetType.IsEnum)
                {
                    if (value is SelectionValue sv) value = sv.Value;
                    converted = value is string s ? Enum.Parse(targetType, s) : Enum.ToObject(targetType, value);
                }
                else
                {
                    converted = Convert.ChangeType(value, targetType);
                }

                if (Equals(_prop.GetValue(_owner), converted)) return;

                _prop.SetValue(_owner, converted);
                OnPropertyChanged();

                if (_prop.Name == "Language")
                    I18N.Ins.Culture = new CultureInfo(converted?.ToString() ?? "en-US");

                if (SelectionValues.Count != 0)
                {
                    var matchingOption = SelectionValues.FirstOrDefault(x => Equals(x.Value, converted));
                    if (!Equals(SelectedValue, matchingOption))
                    {
                        SelectedValue = matchingOption;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Convert Error: {ex.Message}");
            }
        }
    }
}

public partial class SelectionValue : ObservableObject
{
    private readonly string _labelKey;
    public object Value { get; }

    public SelectionValue(object value, string labelKey)
    {
        Value = value;
        _labelKey = labelKey;
    }

    public string Label => Langs.ResourceManager.GetString(_labelKey) ?? _labelKey;
}