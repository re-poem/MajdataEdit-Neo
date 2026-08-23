using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MajdataEdit_Neo.Assets.Langs;
using MajdataEdit_Neo.Base;
using MajdataEdit_Neo.Controls;
using MajdataEdit_Neo.Extensions;
using MajdataEdit_Neo.Models;
using MajdataEdit_Neo.Models.SimaiAnalyzer;
using MajdataEdit_Neo.Types;
using MajdataEdit_Neo.Types.MajSetting;
using MajdataEdit_Neo.Types.Plugin;
using MajdataEdit_Neo.Types.SimaiAnalyzer;
using MajdataEdit_Neo.Utils;
using MajdataEdit_Neo.ViewModels;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using static MajdataEdit_Neo.Base.MajEnv;

namespace MajdataEdit_Neo.Views;

public partial class MainWindow : Window
{
    MainWindowViewModel viewModel => (MainWindowViewModel)DataContext!;

    //window elements
    readonly TextEditor textEditor;
    readonly TextMarkerService markerService;

    readonly SimaiVisualizerControl simaiVisual;

    readonly Button zoomIn, zoomOut;

    readonly NumericUpDown first;
    readonly NumericUpDown speed;


    //behind elements
    readonly DispatcherTimer _debounceTimer;
    readonly SemaphoreSlim _analysisGate = new(1, 1);
    CancellationTokenSource? _analysisCts;
    MainWindowViewModel? _subscribedPlayback;
    bool _isTextChangedBeforeCaretMoving;
    bool _isHandlingCtrlClick;


    string? _currentTooltipMessage;
    private readonly HashSet<Key> _pressedKeys = new();
    bool IsCtrlKeyDown => _pressedKeys.Contains(Key.LeftCtrl) || _pressedKeys.Contains(Key.RightCtrl);

    public MainWindow()
    {
        Console.WriteLine(MajBase);

        var isMac = OperatingSystem.IsMacOS();
        //pull up MajdataView
        var viewPath = MajdataViewExecutableFile;
        if (File.Exists(viewPath) &&
            Process.GetProcessesByName("MajdataViewX").Length <= 0 &&
            Process.GetProcessesByName("Unity").Length <= 0)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = viewPath,
                WorkingDirectory = Path.GetDirectoryName(viewPath)!
            });
        }

        // 补齐mac环境变量
        if (isMac)
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH");
            var extraPath = "/usr/local/bin:/opt/homebrew/bin:/opt/homebrew/sbin";
            Environment.SetEnvironmentVariable("PATH", $"{currentPath}:{extraPath}");
        }

        InitializeComponent();

        //setup editor
        textEditor = this.FindControl<TextEditor>("Editor")!;
        textEditor.TextChanged += TextEditor_TextChanged;
        textEditor.TextArea.TextEntered += TextEditor_TextArea_TextEntered;
        textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        textEditor.TextArea.AddHandler(InputElement.KeyDownEvent, TextEditor_PreviewKeyDown, RoutingStrategies.Tunnel);
        textEditor.TextArea.AddHandler(InputElement.PointerPressedEvent, TextEditor_PreviewPointerPressed, RoutingStrategies.Tunnel);
        textEditor.Options.HighlightCurrentLine = true;
        textEditor.Options.EnableTextDragDrop = true;
        var _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var _install = TextMate.InstallTextMate(textEditor, _registryOptions);
        var registry = new Registry(_install.RegistryOptions);
        _install.SetGrammarFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "simai.tmLanguage.json"));
        markerService = new TextMarkerService(textEditor.Document, textEditor.TextArea.TextView);
        textEditor.TextArea.TextView.BackgroundRenderers.Add(markerService);
        textEditor.PointerMoved += TextEditor_PointerMoved;
        InputMethod.SetIsInputMethodEnabled(textEditor.TextArea, false);
        ConfigureKeyBindings();
        //setup visualizer
        simaiVisual = this.FindControl<SimaiVisualizerControl>("SimaiVisual")!;
        simaiVisual.PointerWheelChanged += SimaiVisual_PointerWheelChanged;
        simaiVisual.PointerMoved += SimaiVisual_PointerMoved;
        //setup zoom buttons
        zoomIn = this.FindControl<Button>("ZoomIn")!;
        zoomIn.Click += ZoomIn_Click;
        zoomOut = this.FindControl<Button>("ZoomOut")!;
        zoomOut.Click += ZoomOut_Click;
        //setup control panel
        first = this.FindControl<NumericUpDown>("First")!;
        first.PointerWheelChanged += First_PointerWheelChanged;
        speed = this.FindControl<NumericUpDown>("Speed")!;
        speed.PointerWheelChanged += Speed_PointerWheelChanged;
        //this window
        this.AddHandler(InputElement.KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Tunnel, true);
        this.AddHandler(InputElement.KeyUpEvent, MainWindow_KeyUp, RoutingStrategies.Tunnel, true);
        this.LostFocus += MainWindow_LostFocus;
        this.Closing += MainWindow_Closing;
        this.Loaded += MainWindow_Loaded;


        //setup debounce timer
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(114.514) };
        _debounceTimer.Tick += _debounceTimer_Tick;

        WeakReferenceMessenger.Default.Register<FocusEditorMsg>(this, (_, _) =>
            Dispatcher.UIThread.Post(() => { textEditor.TextArea.Focus(); })
        );
    }

    private void ConfigureKeyBindings()
    {
        const KeyModifiers ctrl = KeyModifiers.Control;
        const KeyModifiers ctrlShift = KeyModifiers.Control | KeyModifiers.Shift;

        // Keep the complete shortcut layout here so changes do not get scattered
        // between XAML, controls, and plugin-generated menu items.
        var bindings = new (KeyGesture Gesture, Action Execute)[]
        {
            (new(Key.S, ctrl), () => viewModel.SaveFileCommand.Execute(null)),
            (new(Key.Z, ctrl), () => textEditor.Document.UndoStack.Undo()),
            (new(Key.Y, ctrl), () => textEditor.Document.UndoStack.Redo()),

            (new(Key.C, ctrlShift), () => viewModel.PlayStopCommand.Execute(null)),
            (new(Key.X, ctrlShift), () => viewModel.PlayPauseCommand.Execute(null)),
            (new(Key.Z, ctrlShift), () => viewModel.PlayIncludeOpCommand.Execute(null)),

            (new(Key.P, ctrl), () => viewModel.IncreasePlaybackSpeedCommand.Execute(null)),
            (new(Key.O, ctrl), () => viewModel.DecreasePlaybackSpeedCommand.Execute(null)),

            (new(Key.J, ctrl), () => ExecutePluginAction("mirror_h")),
            (new(Key.K, ctrl), () => ExecutePluginAction("mirror_v")),
            (new(Key.L, ctrl), () => ExecutePluginAction("mirror_180")),
            (new(Key.OemSemicolon, ctrl), () => ExecutePluginAction("rotate_r")),
            (new(Key.OemQuotes, ctrl), () => ExecutePluginAction("rotate_l"))
        };

        foreach (var (gesture, execute) in bindings)
        {
            KeyBindings.Add(new KeyBinding
            {
                Gesture = gesture,
                Command = new RelayCommand(execute)
            });
        }
    }

    private void ExecutePluginAction(string iconKey)
    {
        var action = viewModel.PluginItems
            .OfType<PluginAction>()
            .FirstOrDefault(item => item.IconKey == iconKey);

        if (action is not null)
        {
            ViewModel_RequestPluginActionExecution(action);
        }
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        var setting = viewModel.Settings.WindowSetting;
        this.Position = new PixelPoint(setting.PosX, setting.PosY);
        this.Width = setting.Width;
        this.Height = setting.Height;

        LoadPluginsToMenu();
        viewModel.RequestPluginActionExecution += ViewModel_RequestPluginActionExecution;

        if (viewModel.Settings.EditSetting.AutoCheckUpdatesOnStartup)
        {
            await viewModel.CheckUpdateAsync(true);
        }
        await viewModel.ConnectToPlayerAsync();
    }

    private void LoadPluginsToMenu()
    {
        MenuItem editMenu = this.FindControl<MenuItem>("EditMenu")!;
        MenuFlyout editorFlyout = (MenuFlyout)this.FindControl<TextEditor>("Editor")!.ContextFlyout!;

        foreach (var item in viewModel.PluginItems)
        {
            if (item is PluginAction action)
            {
                var geometry = string.IsNullOrEmpty(action.IconKey) ? null
                    : Converters.IconKeyToStreamGeometryConverter.Instance.Convert(
                        action.IconKey,
                        typeof(Avalonia.Media.StreamGeometry),
                        null,
                        System.Globalization.CultureInfo.CurrentCulture)
                    as Avalonia.Media.StreamGeometry;

                var editMenuItem = new MenuItem
                {
                    Header = action.Name,
                    Command = viewModel.ExecutePluginActionCommand,
                    CommandParameter = action,
                    Icon = geometry != null ? new PathIcon { Data = geometry } : null
                };
                var flyoutMenuItem = new MenuItem
                {
                    Header = action.Name,
                    Command = viewModel.ExecutePluginActionCommand,
                    CommandParameter = action,
                    Icon = geometry != null ? new PathIcon { Data = geometry } : null
                };

                editMenu.Items.Add(editMenuItem);
                editorFlyout.Items.Add(flyoutMenuItem);
            }
            else if (item is PluginMenuSeparator)
            {
                editMenu!.Items.Add(new Separator());
                editorFlyout!.Items.Add(new Separator());
            }
        }
    }

    private void ViewModel_RequestPluginActionExecution(PluginAction action)
    {
        if (action.Transform == null) return;

        var selectedText = textEditor.SelectedText;
        if (!string.IsNullOrEmpty(selectedText))
        {
            var newText = action.Transform(selectedText);
            if (newText != selectedText)
            {
                textEditor.Document.Replace(textEditor.SelectionStart, textEditor.SelectionLength, newText);
            }
        }
    }

    bool haveAsked = false;
    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (haveAsked) return;
        e.Cancel = true;
        haveAsked = true;
        viewModel.SetWindowLastState(this);
        var shouldClose = !await viewModel.AskSave();
        if (shouldClose)
        {
            var viewx = Process.GetProcessesByName("MajdataViewX");
            if (viewx.Length > 0)
            {
                var result = await MessageBox.ShowWindowDialogAsync(
                    Langs.Msg_AskCloseView,
                    Langs.Gui_Info,
                    ButtonEnum.YesNo,
                    MsBox.Avalonia.Enums.Icon.Info);
                if (result == ButtonResult.Yes)
                {
                    viewx.FirstOrDefault()?.Kill();
                }
            }

            DetachEventHandlers();
            await viewModel.OnWindowClosingAsync();
            this.Close();
        }
        else haveAsked = false;
    }

    private void MainWindow_LostFocus(object? sender, RoutedEventArgs e)
    {
        _pressedKeys.Clear();
    }

    private void MainWindow_KeyUp(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Remove(e.Key);
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && viewModel.CancelCurrentFFmpeg())
        {
            e.Handled = true;
            return;
        }

        _pressedKeys.Add(e.Key);
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_isTextChangedBeforeCaretMoving || _isHandlingCtrlClick)
        {
            // _isTextChangedBeforeCaretMoving = false;
            // in TextEditor_DebouncedTextChanged
            return;
        }

        var seek = textEditor.CaretOffset;
        UpdateCaretPosition(seek, IsCtrlKeyDown);
    }

    static double? lastX = null;
    private void SimaiVisual_PointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as SimaiVisualizerControl);
        var x = point.Position.X;
        viewModel.IsPointerPressedSimaiVisual = point.Properties.IsLeftButtonPressed;
        if (lastX is null) lastX = x;
        var delta = x - lastX;
        if (point.Properties.IsLeftButtonPressed)
        {
            var docseek = viewModel.SlideTrackTime((float)delta * 10f / Width, viewModel.SongTrackInfo, viewModel.CurrentChartData, viewModel.CurrentSimaiFile?.Offset ?? 0);
            SeekToDocPos(docseek, textEditor);
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

    private void First_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        first.Value += (decimal)(e.Delta.Y / 100d);
        e.Handled = true;
    }

    private void Speed_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var value = speed.Value + (decimal)(e.Delta.Y / 10d);
        if (value < (decimal)0.1)
        {
            e.Handled = true;
            return;
        }
        else
        {
            speed.Value = value;
            e.Handled = true;
        }
    }

    private void SimaiVisual_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (IsCtrlKeyDown)
        {
            viewModel.SlideZoomLevel(-0.3f * (float)e.Delta.Y);
        }
        else
        {
            var docseek = viewModel.SlideTrackTime(e.Delta.Y, viewModel.SongTrackInfo, viewModel.CurrentChartData, (viewModel.CurrentSimaiFile?.Offset ?? 0));
            SeekToDocPos(docseek, textEditor);
        }
    }

    private void TextEditor_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var area = textEditor.TextArea;

        bool hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        //fix: when selection is not empty, left/right key will move caret to start/end of selection,
        //instead of moving caret from the start by one char.
        if (!area.Selection.IsEmpty && !hasShift)
        {
            if (e.Key == Key.Right)
            {
                int endOffset = area.Selection.SurroundingSegment.EndOffset;
                area.Caret.Offset = endOffset;
                area.ClearSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                int startOffset = area.Selection.SurroundingSegment.Offset;
                area.Caret.Offset = startOffset;
                area.ClearSelection();
                e.Handled = true;
            }
        }

        //fix: SB AvaloniaEdit ate my ctrl+up/down
        if (hasCtrl && !hasShift)
        {
            if (e.Key == Key.Up)
            {
                EditingCommands.MoveUpByLine.Execute(null, area);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                EditingCommands.MoveDownByLine.Execute(null, area);
                e.Handled = true;
            }
        }

        //fix: ctrl+left/right/up/down jumps something, we dont need this
        if (hasCtrl)
        {
            if (hasShift)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        EditingCommands.SelectLeftByCharacter.Execute(null, area);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        EditingCommands.SelectRightByCharacter.Execute(null, area);
                        e.Handled = true;
                        break;
                    case Key.Up:
                        EditingCommands.SelectUpByLine.Execute(null, area);
                        e.Handled = true;
                        break;
                    case Key.Down:
                        EditingCommands.SelectDownByLine.Execute(null, area);
                        e.Handled = true;
                        break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Left:
                        EditingCommands.MoveLeftByCharacter.Execute(null, area);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        EditingCommands.MoveRightByCharacter.Execute(null, area);
                        e.Handled = true;
                        break;
                        // up/down is normal
                }
            }
        }
    }
    private void TextEditor_PreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        //fix: sb avalonia edit 整词拖选
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
            e.KeyModifiers.HasFlag(KeyModifiers.Shift) ||
            !e.GetCurrentPoint(textEditor.TextArea).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var textView = textEditor.TextArea.TextView;
        var visualPosition = textView.GetPosition(e.GetPosition(textView) + textView.ScrollOffset);
        if (visualPosition is null)
        {
            return;
        }

        var offset = textEditor.Document.GetOffset(visualPosition.Value.Line, visualPosition.Value.Column);
        _isHandlingCtrlClick = true;
        try
        {
            textEditor.Select(offset, 0);
            textEditor.TextArea.Focus();
        }
        finally
        {
            _isHandlingCtrlClick = false;
        }

        UpdateCaretPosition(offset, true);
        e.Handled = true;
    }

    private void TextEditor_TextChanged(object? sender, EventArgs e)
    {
        _analysisCts?.Cancel();
        _isTextChangedBeforeCaretMoving = true;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }
    private void _debounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        TextEditor_DebouncedTextChanged();
    }
    private async void TextEditor_DebouncedTextChanged()
    {
        var analysisCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _analysisCts, analysisCts);
        previousCts?.Cancel();
        var cancellationToken = analysisCts.Token;
        var enteredGate = false;

        try
        {
            await _analysisGate.WaitAsync(cancellationToken);
            enteredGate = true;
            cancellationToken.ThrowIfCancellationRequested();

            await viewModel.SetFumenContent(textEditor.Text);
            cancellationToken.ThrowIfCancellationRequested();

            var seek = textEditor.CaretOffset;
            // Text edits update parsed caret timing, but must not seek playback.
            UpdateCaretPosition(seek, false);
            _isTextChangedBeforeCaretMoving = false;

            var fumen = viewModel
                .CurrentChartMetadata[viewModel.SelectedDifficulty]
                .Fumen;
            var diags = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = SimaiChecker.Check(fumen);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            viewModel.SimaiDiagnostics = diags;
            markerService.UpdateDiags(diags);

            var signatures = new List<(double, int, int)>();
            var timingList = viewModel.CurrentChartData.CommaTimings;
            if (timingList.Length > 0)
            {
                var firstTiming = timingList[0];
                var lastNum = firstTiming.SignatureNumerator;
                var lastDeno = firstTiming.SignatureDenominator;
                signatures.Add((firstTiming.Timing, lastNum, lastDeno));

                for (var i = 1; i < timingList.Length; i++)
                {
                    var timing = timingList[i];
                    if (timing.SignatureNumerator != lastNum || timing.SignatureDenominator != lastDeno)
                    {
                        signatures.Add((timing.Timing, timing.SignatureNumerator, timing.SignatureDenominator));
                        lastNum = timing.SignatureNumerator;
                        lastDeno = timing.SignatureDenominator;
                    }
                }
            }
            else
            {
                signatures.Add((0, 4, 4));
            }

            viewModel.Signatures = signatures;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Chart analysis failed: {ex}");
            _isTextChangedBeforeCaretMoving = false;
        }
        finally
        {
            if (enteredGate)
                _analysisGate.Release();
            Interlocked.CompareExchange(ref _analysisCts, null, analysisCts);
            analysisCts.Dispose();
        }
    }
    private void TextEditor_PointerMoved(object? sender, PointerEventArgs e)
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

    private void TextEditor_TextArea_TextEntered(object? sender, TextInputEventArgs e)
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

    private async void FindReplace_PointerPressed(object? sender, PointerPressedEventArgs e)
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
    private void SeekToDocPos(Point position, TextEditor editor)
    {
        if (position.Y + 1 > editor.Document.LineCount) return;
        var offset = editor.Document.GetOffset((int)position.Y + 1, (int)position.X);
        editor.Select(offset, 0);
        editor.ScrollTo((int)position.Y + 1, (int)position.X);
        editor.Focus();
    }

    private void UpdateCaretPosition(int offset, bool setTrackTime)
    {
        viewModel.SetCaretPosition(
            offset,
            textEditor.TextArea.Caret.Line,
            setTrackTime);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_subscribedPlayback is not null)
            _subscribedPlayback.RequestSeekToDocPos -= Playback_RequestSeekToDocPos;

        if (DataContext is MainWindowViewModel vm)
        {
            _subscribedPlayback = vm;
            _subscribedPlayback.RequestSeekToDocPos += Playback_RequestSeekToDocPos;
        }
    }

    private void Playback_RequestSeekToDocPos(Point point)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            SeekToDocPos(point, textEditor);
        });
    }

    private void DetachEventHandlers()
    {
        _debounceTimer.Stop();
        _analysisCts?.Cancel();
        viewModel.CancelCurrentFFmpeg();
        viewModel.RequestPluginActionExecution -= ViewModel_RequestPluginActionExecution;
        if (_subscribedPlayback is not null)
        {
            _subscribedPlayback.RequestSeekToDocPos -= Playback_RequestSeekToDocPos;
            _subscribedPlayback = null;
        }
    }
}
