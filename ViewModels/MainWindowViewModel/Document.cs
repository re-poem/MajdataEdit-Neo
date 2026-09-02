using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_Neo.Types;
using MajdataEdit_Neo.Types.SimaiAnalyzer;
using MajSimai;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MajdataEdit_Neo.ViewModels;

/// <summary>
/// 谱面文档管理
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
    public partial SimaiFile? CurrentSimaiFile { get; set; } = null;

    partial void OnCurrentSimaiFileChanged(SimaiFile? value) => RefreshFumenDocument();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(Designer))]
    [NotifyPropertyChangedFor(nameof(CurrentFumen))]
    public partial int SelectedDifficulty { get; set; } = 0;

    partial void OnSelectedDifficultyChanged(int value) => RefreshFumenDocument();

    [ObservableProperty]
    internal partial MutSimaiChartMetadata[] CurrentChartMetadata { get; set; } = new MutSimaiChartMetadata[7];

    [ObservableProperty]
    public partial SimaiChart CurrentChartData { get; set; } = SimaiChart.Empty;
    public TextDocument FumenDocument => _fumenDocument;

    //------editor state

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitleSuffix))]
    public partial bool IsSaved { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimaiDiagnosticsCount))]
    public partial IReadOnlyList<SimaiDiagnostic> SimaiDiagnostics { get; set; }

    [ObservableProperty]
    public partial List<(double, int, int)> Signatures { get; set; } = [(0, 4, 4)];

    //------internal state

    internal readonly TextDocument _fumenDocument = new();
    internal readonly Lock _fumenContentChangedSyncLock = new();
    readonly string[] _level = new string[7];
    float _offset = 0;

    public string OriginFumen { get; set; } = string.Empty;

    //------derived properties

    public bool IsLoaded => CurrentSimaiFile is not null;

    public int SimaiDiagnosticsCount =>
        SimaiDiagnostics?.Count(o => o.Severity == Severity.Error) ?? 0;

    /// <summary>
    /// 用于WindowTitle的后缀部分（标题 + 保存标记）
    /// </summary>
    public string WindowTitleSuffix
    {
        get
        {
            if (CurrentSimaiFile is null) return "";
            return $" - {CurrentSimaiFile.Title}" + (IsSaved ? "" : "*");
        }
    }


    public string CurrentFumen
    {
        get
        {
            if (CurrentSimaiFile is null)
                return string.Empty;
            return CurrentChartMetadata[SelectedDifficulty].Fumen;
        }
    }

    public float Offset
    {
        get
        {
            if (CurrentSimaiFile is null) return _offset;
            _offset = CurrentSimaiFile.Offset;
            return _offset;
        }
        set
        {
            if (CurrentSimaiFile is null) return;
            CurrentSimaiFile.Offset = value;
            SetProperty(ref _offset, value);
            OnPropertyChanged(nameof(CurrentSimaiFile));
            _ = PushUpdateAsync();
        }
    }

    public string Level
    {
        get
        {
            if (CurrentSimaiFile is null || CurrentChartMetadata[SelectedDifficulty] is null) return "";
            _level[SelectedDifficulty] = CurrentChartMetadata[SelectedDifficulty].Level;
            return _level[SelectedDifficulty];
        }
        set
        {
            if (CurrentSimaiFile is null || CurrentChartMetadata[SelectedDifficulty] is null) return;
            CurrentChartMetadata[SelectedDifficulty].Level = value;
            Debug.WriteLine(SelectedDifficulty);
            SetProperty(ref _level[SelectedDifficulty], value);
            OnPropertyChanged(nameof(CurrentSimaiFile));
        }
    }

    public string Designer
    {
        get
        {
            if (CurrentSimaiFile is null || CurrentChartMetadata[SelectedDifficulty] is null) return "";
            var text = CurrentChartMetadata[SelectedDifficulty].Designer;
            if (text is null) return "";
            return text;
        }
        set
        {
            if (CurrentSimaiFile is null || CurrentChartMetadata[SelectedDifficulty] is null) return;
            var text = CurrentChartMetadata[SelectedDifficulty].Designer;
            if (text is null) return;
            SetProperty(ref text, value);
            CurrentChartMetadata[SelectedDifficulty].Designer = text;
            OnPropertyChanged(nameof(CurrentSimaiFile));
        }
    }

    /// <summary>
    /// IsFumenContextChanged 的行为：同时更新 IsSaved 和 AutoSave 的 IsFileChanged
    /// </summary>
    public bool IsFumenContextChanged
    {
        get => !IsSaved;
        set => IsSaved = !value;
    }

    //------initialization

    private void InitializeDocument()
    {
        for (var i = 0; i < 7; i++) CurrentChartMetadata[i] = new MutSimaiChartMetadata();
    }

    //------methods

    public void RefreshFumenDocument()
    {
        if (CurrentSimaiFile is null)
        {
            CurrentChartData = SimaiChart.Empty;
            if (_fumenDocument.Text != string.Empty)
            {
                _fumenDocument.Text = string.Empty;
                _fumenDocument.UndoStack.ClearAll();
            }
            OriginFumen = string.Empty;
            return;
        }

        var difficulty = SelectedDifficulty;
        var metadata = CurrentChartMetadata[difficulty];
        var fumenContent = metadata.Fumen ?? string.Empty;
        OriginFumen = fumenContent;
        CurrentChartData = string.IsNullOrEmpty(fumenContent)
            ? SimaiChart.Empty
            : CurrentSimaiFile.Charts[difficulty];

        if (_fumenDocument.Text != fumenContent)
        {
            _fumenDocument.Text = fumenContent;
            _fumenDocument.UndoStack.ClearAll();
        }
    }

    public async Task SetFumenContent(string content)
    {
        var simaiFile = CurrentSimaiFile;
        if (simaiFile is null) return;
        content ??= string.Empty;

        var difficulty = SelectedDifficulty;
        var metadata = CurrentChartMetadata[difficulty];
        metadata.Fumen = content;
        simaiFile.Charts[difficulty] = new SimaiChart(
            metadata.Level,
            metadata.Designer,
            content,
            ReadOnlySpan<SimaiTimingPoint>.Empty,
            ReadOnlySpan<SimaiTimingPoint>.Empty);
        UpdateFumenContextChanged();

        if (string.IsNullOrEmpty(content))
        {
            simaiFile.Charts[difficulty] = SimaiChart.Empty;
            if (ReferenceEquals(CurrentSimaiFile, simaiFile) && SelectedDifficulty == difficulty)
                CurrentChartData = SimaiChart.Empty;
            FumenContentChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            var data = await SimaiParser.ParseChartAsync(metadata.Level, metadata.Designer, content);

            if (!ReferenceEquals(CurrentSimaiFile, simaiFile) ||
                CurrentChartMetadata[difficulty].Fumen != content)
            {
                return;
            }

            simaiFile.Charts[difficulty] = data;
            if (SelectedDifficulty == difficulty)
                CurrentChartData = data;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        FumenContentChanged?.Invoke(this, EventArgs.Empty);
    }

    public SimaiTimingPoint? GetNearestCommaTimingFromPos(int rawPosition)
    {
        var timings = CurrentChartData.CommaTimings;
        if (timings.Length == 0) return null;

        SimaiTimingPoint nearestTiming = timings[0];
        foreach (var timing in timings)
        {
            if (timing.RawTextPosition >= rawPosition)
            {
                nearestTiming = timing;
                break;
            }
        }
        return nearestTiming;
    }

    /// <summary>
    /// 检查fumen内容是否已变更
    /// </summary>
    public void UpdateFumenContextChanged()
    {
        lock (_fumenContentChangedSyncLock)
        {
            IsFumenContextChanged = OriginFumen != CurrentFumen;
        }
    }

    /// <summary>
    /// 标记为已保存
    /// </summary>
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
        OnPropertyChanged(nameof(CurrentSimaiFile));
    }
}
