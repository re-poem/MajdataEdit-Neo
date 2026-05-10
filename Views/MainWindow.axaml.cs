using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using AvaloniaEdit.Utils;
using MajdataEdit_Neo.Controls;
using MajdataEdit_Neo.Models.SimaiAnalyzer;
using MajdataEdit_Neo.Types.MajSetting;
using MajdataEdit_Neo.Types.SimaiAnalyzer;
using MajdataEdit_Neo.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace MajdataEdit_Neo.Views;

public partial class MainWindow : Window
{
    MainWindowViewModel viewModel => (MainWindowViewModel)DataContext;
    TextEditor textEditor;
    TextMarkerService markerService;
    SimaiVisualizerControl simaiVisual;

    DispatcherTimer _debounceTimer;
    string? _currentTooltipMessage;
    private List<double> popupBpms = new();
    private DateTime popupLastTapTime = DateTime.MinValue;
    public MainWindow()
    {
        //pull up MajdataView
        var viewPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "MajdataView.exe");

        if (File.Exists(viewPath) &&
            Process.GetProcessesByName("MajdataView").Length <= 0)
        {
            Process.Start(viewPath);
        }

        InitializeComponent();
        //setup editor
        textEditor = this.FindControl<TextEditor>("Editor");
        textEditor.TextChanged += TextEditor_TextChanged;
        textEditor.TextArea.TextEntered += TextEditor_TextArea_TextEntered;
        textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        textEditor.Options.HighlightCurrentLine = true;
        textEditor.Options.EnableTextDragDrop = true;
        var _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var _install = TextMate.InstallTextMate(textEditor, _registryOptions);
        var registry = new Registry(_install.RegistryOptions);
        _install.SetGrammarFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "simai.tmLanguage.json"));
        _debounceTimer = new DispatcherTimer{ Interval = TimeSpan.FromMilliseconds(114.5) };
        _debounceTimer.Tick += _debounceTimer_Tick;
        markerService = new TextMarkerService(textEditor.Document, textEditor.TextArea.TextView);
        textEditor.TextArea.TextView.BackgroundRenderers.Add(markerService);
        textEditor.PointerMoved += TextEditor_PointerMoved;
        //setup visualizer
        simaiVisual = this.FindControl<SimaiVisualizerControl>("SimaiVisual");
        simaiVisual.PointerWheelChanged += SimaiVisual_PointerWheelChanged;
        simaiVisual.PointerMoved += SimaiVisual_PointerMoved;
        //zoom buttons
        this.FindControl<Button>("ZoomIn").Click += ZoomIn_Click;
        this.FindControl<Button>("ZoomOut").Click += ZoomOut_Click;
        //control panel
        First.PointerWheelChanged += First_PointerWheelChanged;
        //this window
        this.KeyDown += MainWindow_KeyDown;
        this.KeyUp += MainWindow_KeyUp;
        this.LostFocus += MainWindow_LostFocus;
        this.Closing += MainWindow_Closing;
        this.Loaded += MainWindow_Loaded;
    }


    private void Tap_Button_Click(object? sender, RoutedEventArgs e)
    {
        if (popupLastTapTime != DateTime.MinValue)
        {
            var delta = (DateTime.Now - popupLastTapTime).TotalSeconds;
            popupBpms.Add(60d / delta);
        }

        popupLastTapTime = DateTime.Now;
        if (popupBpms.Count <= 0) return;
        if (popupBpms.Count > 20)
        {
            popupBpms.Remove(popupBpms.Min());
            popupBpms.Remove(popupBpms.Max());
        }

        var avg = popupBpms.Sum() / popupBpms.Count;
        PopupTapButtonText.Content = string.Format("{0:N1}", avg);
    }

    private void Reset_Button_Click(object? sender, RoutedEventArgs e)
    {
        popupBpms = new List<double>();
        popupLastTapTime = DateTime.MinValue;
        PopupTapButtonText.Content = "Tap";
    }

    private void PopupBackdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        viewModel.ClosePopup();
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        var setting = viewModel.Settings.WindowSetting;
        this.Position = new PixelPoint(setting.PosX, setting.PosY);
        this.Width = setting.Width;
        this.Height = setting.Height;
        await viewModel.ConnectToPlayerAsync();
    }

    bool haveAsked = false;
    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        viewModel.SetWindowLastState(this);
        if (haveAsked) return;
        e.Cancel = true;
        haveAsked = true;
        if (!await viewModel.AskSave())
        {
            Process.GetProcessesByName("MajdataView").FirstOrDefault()?.Kill();
            this.Close();
        }
        else haveAsked = false;
    }

    private void MainWindow_LostFocus(object? sender, RoutedEventArgs e)
    {
        isCtrlKeyDown = false;
    }

    bool isCtrlKeyDown = false;

    private void MainWindow_KeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        isCtrlKeyDown = false;
    }

    private void MainWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        isCtrlKeyDown = e.Key == Avalonia.Input.Key.LeftCtrl;
    }

    private void Caret_PositionChanged(object? sender, System.EventArgs e)
    {
        var seek = textEditor.SelectionStart;
        viewModel.SetCaretTime(seek, isCtrlKeyDown);
        viewModel.CaretLine = textEditor.TextArea.Caret.Line;
    }

    static double? lastX = null;
    private void SimaiVisual_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as SimaiVisualizerControl);
        var x = point.Position.X;
        viewModel.IsPointerPressedSimaiVisual = point.Properties.IsLeftButtonPressed;
        if (lastX is null) lastX = x;
        var delta = x - lastX;
        if (point.Properties.IsLeftButtonPressed)
        {
            var docseek = viewModel.SlideTrackTime((float)delta*10f/Width);
            viewModel.SeekToDocPos(docseek,textEditor);
        }
        lastX = x;
    }

    private void ZoomIn_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.SlideZoomLevel(-0.3f);
    }
    private void ZoomOut_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.SlideZoomLevel(0.3f);
    }

    private void First_PointerWheelChanged(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        First.Value += (decimal)(e.Delta.Y / 100d);
    }

    private void SimaiVisual_PointerWheelChanged(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        if (isCtrlKeyDown)
        {
            viewModel.SlideZoomLevel(-0.3f * (float)e.Delta.Y);
        }
        else
        {
            var docseek = viewModel.SlideTrackTime(e.Delta.Y);
            viewModel.SeekToDocPos(docseek,textEditor);
        }
    }

    private async void TextEditor_TextChanged(object? sender, System.EventArgs e)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
        await viewModel.SetFumenContent(((TextEditor)sender).Text);
        var seek = textEditor.SelectionStart;
        viewModel.SetCaretTime(seek,false);
    }
    private void _debounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        TextEditor_DebouncedTextChanged();
    }
    private async void TextEditor_DebouncedTextChanged()
    {
        var fumen = viewModel.CurrentFumen;

        var diags = await Task.Run(() => SimaiChecker.Check(fumen));
        viewModel.SimaiDiagnostics = diags;
        markerService.UpdateDiags(diags);

        var annos = await Task.Run(() => SimaiAnnotationParser.Parse(fumen));
        if (!annos.Any()) return;
        viewModel.Signatures.Clear();
        foreach (var annotation in annos)
        {
            switch (annotation)
            {
                case SignatureAnnotation s:
                    var timing = viewModel.GetNearestCommaTimingFromPos(s.Position);
                    if (timing == null) continue;

                    viewModel.Signatures.Add((timing.Timing, s.Numerator, s.Denominator));
                    break;
            }
        }
    }
    private void TextEditor_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        var textView = textEditor.TextArea.TextView;
        var pos = e.GetPosition(textView);
        var visualPos = textView.GetPosition(pos + textView.ScrollOffset);

        string? newMessage = null;
        if (visualPos != null)
        {
            int offset = textEditor.Document.GetOffset(visualPos.Value.Line, visualPos.Value.Column);
            var marker = markerService.GetMarkerAtOffset(offset);
            newMessage = marker?.Message;
        }
        
        if (_currentTooltipMessage != newMessage)
        {
            _currentTooltipMessage = newMessage;
            if (!string.IsNullOrEmpty(newMessage))
            {
                ToolTip.SetTip(textEditor.TextArea, newMessage);
                ToolTip.SetIsOpen(textEditor.TextArea, true);
            }
            else
            {
                ToolTip.SetIsOpen(textEditor.TextArea, false);
            }
        }
    }

    private void TextEditor_TextArea_TextEntered(object? sender, Avalonia.Input.TextInputEventArgs e)
    {
        if (SimaiCompletionData.SIMAI_COMPLETIONS.ContainsKey(e.Text?[0] ?? '\0'))
        {
            var completionWindow = new CompletionWindow(textEditor.TextArea);
            completionWindow.Closed += (o, args) => completionWindow = null;

            var data = completionWindow.CompletionList.CompletionData;
            data.AddRange(SimaiCompletionData.SIMAI_COMPLETIONS[e.Text![0]]);

            completionWindow.Show();
        }
    }

    private async void FindReplace_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (textEditor.SearchPanel.IsOpened)
            textEditor.SearchPanel.Close();
        else
        {
            textEditor.TextArea.Focus();
            await Task.Delay(100); // focus will cost time, or the searchpanel buttons wont work.
            textEditor.SearchPanel.Open();
        }
    }


}
