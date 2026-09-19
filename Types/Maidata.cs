using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cimai;
using MajdataEdit_Neo.Extensions;

namespace MajdataEdit_Neo.Types;

public sealed class MaidataFile : IDisposable
{
    // 持有timings数据
    private SimaiChart?[] _charts = new SimaiChart?[7];

    public bool IsEmpty { get; private set; }
    public static MaidataFile Empty = new() { IsEmpty = true };
    private MaidataFile() { }

    //----文档级元数据 (managed 字符串，独立于 Cimai native 生命周期)

    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Designer { get; set; } = string.Empty;
    public float Offset { get; set; }
    public List<MutSimaiCommand> Commands { get; } = new();



    public string[] Levels { get; } = new string[7];
    public string[] Designers { get; } = new string[7];
    public string[] Fumens { get; } = new string[7];

    // timings数据入口，只能访问timings，别的是无效数据
    public SimaiChart GetChart(int difficulty)
        => IsEmpty
            ? SimaiChart.Empty
            : _charts[difficulty] is { IsDisposed: false } c ? c : SimaiChart.Empty;


    /// <summary>Fumen 改动后调用：替换指定难度的 Chart 并释放旧实例。
    /// 本文件已 Dispose 时直接 no-op，避免对已 free 的 arena 二次释放。</summary>
    internal void ReplaceChart(int difficulty, SimaiChart chart)
    {
        if (IsEmpty)
        {
            chart.Dispose();
            return;
        }
        var old = _charts[difficulty];
        _charts[difficulty] = chart;
        old?.Dispose();
    }

    public void Dispose()
    {
        if (IsEmpty) return;
        IsEmpty = true;
        foreach (var chart in _charts)
            chart?.Dispose();
        _charts = new SimaiChart?[7];
    }



    public static MaidataFile Parse(string text)
    {
        using var simai = SimaiFile.Parse(text);

        var file = new MaidataFile
        {
            Title = simai.Title.Utf8String(),
            Artist = simai.Artist.Utf8String(),
            Designer = simai.Des.Utf8String(),
            Offset = simai.Offset,
        };
        foreach (var cmd in simai.Commands)
            file.Commands.Add(new MutSimaiCommand(cmd.Key.Utf8String(), cmd.Value.Utf8String()));

        for (int i = 0; i < (int)SimaiDifficulty.DIFFICULTY_COUNT; i++)
        {
            var chart = simai.Charts[i];
            if (chart == null) continue;
            file.Levels[i] = chart.Level.Utf8String();
            file.Designers[i] = chart.Des.Utf8String();
            var fumen = chart.Fumen.Utf8String();
            file.Fumens[i] = fumen;
            // Parse包含timings数据，但File free时会清理掉它们，要给它们独立一下
            simai.DetachChart((SimaiDifficulty)i);
            file.ReplaceChart(i, chart);
        }

        return file;
    }

    public static string Deparse(MaidataFile file)
    {
        var sb = new StringBuilder()
            .Append("&title=").AppendLine(file.Title)
            .Append("&artist=").AppendLine(file.Artist)
            .Append("&des=").AppendLine(file.Designer)
            .Append("&first=").AppendLine(file.Offset.ToString(CultureInfo.InvariantCulture));

        for (var i = 0; i < 7; i++)
        {
            if (!string.IsNullOrEmpty(file.Designers[i]))
                sb.Append("&des_").Append(i + 1).Append('=').AppendLine(file.Designers[i]);
            if (!string.IsNullOrEmpty(file.Levels[i]))
                sb.Append("&lv_").Append(i + 1).Append('=').AppendLine(file.Levels[i]);
        }

        foreach (var cmd in file.Commands)
            sb.Append('&').Append(cmd.Key).Append('=').AppendLine(cmd.Value);

        for (var i = 0; i < 7; i++)
        {
            var fumen = file.Fumens[i];
            if (!string.IsNullOrEmpty(fumen))
                sb.Append("&inote_").Append(i + 1).Append('=').Append(fumen).AppendLine();
        }

        return sb.ToString();
    }
}

/// <summary>可变命令项 (&amp;key=value)，字段名跟 Cimai 对齐。Deparse 时展开成 maidata 文本。</summary>
public sealed class MutSimaiCommand(string key, string value)
{
    public string Key { get; set; } = key;
    public string Value { get; set; } = value;
}