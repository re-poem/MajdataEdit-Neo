using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_Neo.Assets.Langs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MajdataEdit_Neo;

public partial class I18N : ObservableObject
{
    public static I18N Ins { get; } = new();

    [ObservableProperty]
    public partial CultureInfo Culture { get; set; }

    partial void OnCultureChanged(CultureInfo value)
    {
        Langs.Culture = value;
        OnPropertyChanged("Item[]");
    }
}

// Usage: {I18N Gui_Difficulty} or {I18N {Binding DisplayKey}}
//           (Static)                  (Dynamic)
public class I18NExtension : MarkupExtension
{
    public object Key { get; set; }

    public I18NExtension(object key) => Key = key;


    private static readonly IMultiValueConverter Converter =
        new FuncMultiValueConverter<object, string>(values =>
        {
            if (values.FirstOrDefault() is string key)
            {
                return Langs.ResourceManager.GetString(key, I18N.Ins.Culture) ?? key;
            }
            return "";
        });

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var keyBinding = Key is IBinding b ?
            b : 
            new Binding { Source = Key.ToString() };

        return new MultiBinding
        {
            Converter = Converter,
            Bindings =
            {
                keyBinding,
                // only for refresh when culture changes
                new Binding(nameof(I18N.Ins.Culture)) { Source = I18N.Ins }
            }
        };
    }
}