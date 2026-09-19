using AvaloniaEdit.Document;
using Cimai;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_Neo.Types;
using MajdataEdit_Neo.Types.SimaiAnalyzer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MajdataEdit_Neo.ViewModels;

/// <summary>
/// 谱面文档管理。所有字段都在 MaidataFile 上直接读写；Cimai 的 Chart 实例由
/// MaidataFile 自己保活，外部只通过 CurrentChart 这个引用观察 Chart span 源。
/// </summary>
public partial class MainWindowViewModel
{
    public event EventHandler? FumenContentChanged;

    //------document state

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(Designer))]
    [NotifyPropertyChangedFor(nameof(Offset))]
    [NotifyPropertyChangedFor(nameof(IsLoaded))]
    [NotifyPropertyChangedFor(nameof(CurrentFumen))]
    public partial MaidataFile CurrentMaidata { get; set; } = MaidataFile.Empty;

    [ObservableProperty]
    public partial SimaiChart CurrentChart { get; set; } = SimaiChart.Empty;

    partial void OnCurrentMaidataChanged(MaidataFile oldValue, MaidataFile newValue)
    {
        RefreshFumenDocument();
        if (!ReferenceEquals(oldValue, newValue))
            oldValue.Dispose();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(Designer))]
    [NotifyPropertyChangedFor(nameof(CurrentFumen))]
    public partial int SelectedDifficulty { get; set; } = 0;

    partial void OnSelectedDifficultyChanged(int value) => RefreshFumenDocument();

    public TextDocument FumenDocument => _fumenDocument;

    //------editor state

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitleSuffix))]
    public partial bool IsSaved { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimaiDiagnosticsCount))]
    public partial IReadOnlyList<SimaiDiagnostic> SimaiDiagnostics { get; set; } = Array.Empty<SimaiDiagnostic>();

    [ObservableProperty]
    public partial List<(double, int, int)> Signatures { get; set; } = [(0, 4, 4)];

    //------internal state

    internal readonly TextDocument _fumenDocument = new();
    internal readonly Lock _fumenContentChangedSyncLock = new();

    public string OriginFumen { get; set; } = string.Empty;

    //------derived properties

    public bool IsLoaded => !CurrentMaidata.IsEmpty;

    public int SimaiDiagnosticsCount =>
        SimaiDiagnostics?.Count(o => o.Severity == Severity.Error) ?? 0;

    /// <summary>用于WindowTitle的后缀部分（标题 + 保存标记）</summary>
    public string WindowTitleSuffix =>
        CurrentMaidata.IsEmpty ? "" : $" - {CurrentMaidata.Title}" + (IsSaved ? "" : "*");

    public string CurrentFumen =>
        CurrentMaidata.IsEmpty ? string.Empty : CurrentMaidata.Fumens[SelectedDifficulty];

    public float Offset
    {
        get => CurrentMaidata?.Offset ?? 0;
        set
        {
            if (CurrentMaidata.IsEmpty) return;
            CurrentMaidata.Offset = value;
            OnPropertyChanged(nameof(Offset));
        }
    }

    public string Level
    {
        get => CurrentMaidata.IsEmpty ? "" : CurrentMaidata.Levels[SelectedDifficulty];
        set
        {
            if (CurrentMaidata.IsEmpty) return;
            CurrentMaidata.Levels[SelectedDifficulty] = value;
            OnPropertyChanged(nameof(Level));
        }
    }

    public string Designer
    {
        get => CurrentMaidata.IsEmpty ? "" : CurrentMaidata.Designers[SelectedDifficulty];
        set
        {
            if (CurrentMaidata.IsEmpty) return;
            CurrentMaidata.Designers[SelectedDifficulty] = value;
            OnPropertyChanged(nameof(Designer));
        }
    }

    /// <summary>IsFumenContextChanged 的行为：同时更新 IsSaved 和 AutoSave 的 IsFileChanged</summary>
    public bool IsFumenContextChanged
    {
        get => !IsSaved;
        set => IsSaved = !value;
    }

    //------initialization

    private void InitializeDocument() { }

    //------methods

    public void RefreshFumenDocument()
    {
        if (CurrentMaidata.IsEmpty)
        {
            if (_fumenDocument.Text != string.Empty)
            {
                _fumenDocument.Text = string.Empty;
                _fumenDocument.UndoStack.ClearAll();
            }
            OriginFumen = string.Empty;
            return;
        }

        var difficulty = SelectedDifficulty;
        var fumenContent = CurrentMaidata.Fumens[difficulty] ?? string.Empty;
        OriginFumen = fumenContent;

        if (_fumenDocument.Text != fumenContent)
        {
            _fumenDocument.Text = fumenContent;
            _fumenDocument.UndoStack.ClearAll();
        }
    }

    public async Task SetFumenContent(string content)
    {
        var file = CurrentMaidata;
        if (file is null) return;
        content ??= string.Empty;

        var difficulty = SelectedDifficulty;
        file.Fumens[difficulty] = content;
        UpdateFumenContextChanged();

        try
        {
            var newChart = SimaiChart.Parse(content);
            file.ReplaceChart(difficulty, newChart);
            CurrentChart = newChart;

            if (!ReferenceEquals(CurrentMaidata, file) ||
                file.Fumens[difficulty] != content)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        FumenContentChanged?.Invoke(this, EventArgs.Empty);
    }

    public SimaiTiming? GetNearestCommaTimingFromPos(int rawPosition)
    {
        var timings = CurrentMaidata.IsEmpty ? default : CurrentMaidata.GetChart(SelectedDifficulty).Timings;
        if (timings.Length == 0) return null;

        foreach (var timing in timings)
        {
            if ((ulong)timing.FumenPos >= (ulong)rawPosition)
                return timing;
        }
        return timings[0];
    }

    /// <summary>检查fumen内容是否已变更</summary>
    public void UpdateFumenContextChanged()
    {
        lock (_fumenContentChangedSyncLock)
        {
            IsFumenContextChanged = OriginFumen != CurrentFumen;
        }
    }

    /// <summary>标记为已保存</summary>
    public void MarkAsSaved()
    {
        lock (_fumenContentChangedSyncLock)
        {
            IsFumenContextChanged = false;
            OriginFumen = CurrentFumen;
        }
    }

    public void NotifySimaiFileChanged()
    {
        OnPropertyChanged(nameof(CurrentMaidata));
    }
}
