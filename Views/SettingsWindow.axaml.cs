using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using MajdataEdit_Neo.Types.MajSetting;
using MajdataEdit_Neo.ViewModels;
using System;
using System.Collections.Generic;

namespace MajdataEdit_Neo.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }
}

public class SettingTemplateSelector : Dictionary<SettingControlType, IDataTemplate>, IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is SettingItem item && this.TryGetValue(item.ControlType, out var template))
        {
            return template.Build(param)!;
        }
        return new TextBlock { Text = "Template Not Found" };
    }

    public bool Match(object? data) => data is SettingItem;
}