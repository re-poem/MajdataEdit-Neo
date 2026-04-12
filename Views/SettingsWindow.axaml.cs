using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using System;
using MajdataEdit_Neo.ViewModels;

namespace MajdataEdit_Neo.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }
}

public class SettingTemplateSelector : IDataTemplate
{
    // 这个方法决定如何创建 UI
    public Control Build(object? param)
    {
        var item = param as SettingItem;
        if (item == null) return new TextBlock { Text = "Invalid Data" };

        // 1. 布尔类型 -> CheckBox
        if (item.PropertyType == typeof(bool))
        {
            var cb = new CheckBox { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            cb.Bind(CheckBox.IsCheckedProperty, CreateBinding(item));
            return cb;
        }

        // 2. 枚举类型 -> ComboBox
        if (item.PropertyType.IsEnum)
        {
            var combo = new ComboBox
            {
                Width = 200,
                ItemsSource = item.EnumOptions
            };
            combo.Bind(ComboBox.SelectedItemProperty, CreateBinding(item));
            return combo;
        }

        // 3. 数字类型 -> NumericUpDown
        if (IsNumericType(item.PropertyType))
        {
            var num = new NumericUpDown { Width = 200, Increment = 0.1m };
            num.Bind(NumericUpDown.ValueProperty, CreateBinding(item));
            return num;
        }

        // 4. 默认 -> TextBox
        var tb = new TextBox { Width = 200 };
        tb.Bind(TextBox.TextProperty, CreateBinding(item));
        return tb;
    }

    private Binding CreateBinding(SettingItem item)
    {
        return new Binding("Value")
        {
            Source = item,
            Mode = BindingMode.TwoWay
        };
    }

    private bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(float) ||
               type == typeof(double) || type == typeof(decimal);
    }

    public bool Match(object? data) => data is SettingItem;
}