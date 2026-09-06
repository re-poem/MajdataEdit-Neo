using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MajdataEdit_Neo.Models;
using Cimai;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MajdataEdit_Neo.Controls;

internal class SimaiVisualizerControl : Control
{
    private readonly AnimationState _animationState = new();
    private RenderCache _renderCache = new();
    private bool _renderCacheDisposed;
    private int _animationFramePending;

    //Set the properties
    //The naming of this should be strictly followed "Xxx" and "XxxProperty"
    public static readonly DirectProperty<SimaiVisualizerControl, double> TimeProperty =
    AvaloniaProperty.RegisterDirect<SimaiVisualizerControl, double>(
        nameof(Time),
        o => o.Time,
        (o, v) => o.Time = v,
        defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private double _time;
    public double Time
    {
        get { return _time; }
        set { SetAndRaise(TimeProperty, ref _time, value); }
    }

    public static readonly DirectProperty<SimaiVisualizerControl, TrackInfo?> TrackIfProperty =
    AvaloniaProperty.RegisterDirect<SimaiVisualizerControl, TrackInfo?>(
        nameof(TrackIf),
        o => o.TrackIf,
        (o, v) => o.TrackIf = v,
        defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private TrackInfo? _track;
    public TrackInfo? TrackIf
    {
        get { return _track; }
        set { SetAndRaise(TrackIfProperty, ref _track, value); }
    }

    public static readonly DirectProperty<SimaiVisualizerControl, float> ZoomLevelProperty =
    AvaloniaProperty.RegisterDirect<SimaiVisualizerControl, float>(
        nameof(ZoomLevel),
        o => o.ZoomLevel,
        (o, v) => o.ZoomLevel = v,
        defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private float _zoomLevel;
    public float ZoomLevel
    {
        get { return _zoomLevel; }
        set { SetAndRaise(ZoomLevelProperty, ref _zoomLevel, value); }
    }

    public static readonly DirectProperty<SimaiVisualizerControl, SimaiChart?> SimaiChartProperty =
    AvaloniaProperty.RegisterDirect<SimaiVisualizerControl, SimaiChart?>(
        nameof(SimaiChart),
        o => o.SimaiChart,
        (o, v) => o.SimaiChart = v,
        defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private SimaiChart? _simaiChart;
    public SimaiChart? SimaiChart
    {
        get { return _simaiChart; }
        set { SetAndRaise(SimaiChartProperty, ref _simaiChart, value); }
    }

    public static readonly DirectProperty<SimaiVisualizerControl, List<(double, int, int)>?> SignaturesProperty =
    AvaloniaProperty.RegisterDirect<SimaiVisualizerControl, List<(double, int, int)>?>(
        nameof(Signatures),
        o => o.Signatures,
        (o, v) => o.Signatures = v,
        defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private List<(double, int, int)>? _signatures;
    public List<(double, int, int)>? Signatures
    {
        get { return _signatures; }
        set { SetAndRaise(SignaturesProperty, ref _signatures, value); }
    }

    public static readonly DirectProperty<SimaiVisualizerControl, float> OffsetProperty =
   AvaloniaProperty.RegisterDirect<SimaiVisualizerControl, float>(
       nameof(Offset),
       o => o.Offset,
       (o, v) => o.Offset = v,
       defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private float _offset;
    public float Offset
    {
        get { return _offset; }
        set { SetAndRaise(OffsetProperty, ref _offset, value); }
    }

    public static readonly DirectProperty<SimaiVisualizerControl, double> CaretTimeProperty =
    AvaloniaProperty.RegisterDirect<SimaiVisualizerControl, double>(
        nameof(CaretTime),
        o => o.CaretTime,
        (o, v) => o.CaretTime = v,
        defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private double _caretTime;
    public double CaretTime
    {
        get { return _caretTime; }
        set { SetAndRaise(CaretTimeProperty, ref _caretTime, value); }
    }

    public static readonly DirectProperty<SimaiVisualizerControl, bool> IsAnimatedProperty =
    AvaloniaProperty.RegisterDirect<SimaiVisualizerControl, bool>(
        nameof(IsAnimated),
        o => o.IsAnimated,
        (o, v) => o.IsAnimated = v,
        defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private bool _isAnimated;
    public bool IsAnimated
    {
        get { return _isAnimated; }
        set { SetAndRaise(IsAnimatedProperty, ref _isAnimated, value); }
    }

    public SimaiVisualizerControl()
    {
        ClipToBounds = true;

        AffectsRender<SimaiVisualizerControl>(TimeProperty, TrackIfProperty, ZoomLevelProperty,
            SimaiChartProperty, OffsetProperty, CaretTimeProperty, IsAnimatedProperty);
    }

    private sealed class AnimationState
    {
        public double Time;
        public double Zoom;
    }

    private sealed class RenderCache : IDisposable
    {
        public readonly SKTypeface Typeface = SKTypeface.FromFamilyName(
            OperatingSystem.IsWindows()
                ? "Consolas"
                : OperatingSystem.IsMacOS()
                    ? "Menlo"
                    : "monospace",
            SKFontStyle.Bold);
        public readonly SKFont TextFont;
        public readonly SKPaint Paint = new();
        public readonly SKPaint HanabiPaint = new() { Style = SKPaintStyle.Fill };
        public readonly SKShader HanabiShader;
        public readonly SKPath CursorPath = new();
        public readonly SKPath WavePath = new();
        public readonly List<SKPoint> WavePoints = new(1024);
        public readonly List<double> BpmChangeTimes = new(32);
        public readonly List<float> BpmChangeValues = new(32);
        public readonly List<double> StrongBeats = new(64);
        public readonly List<double> WeakBeats = new(128);
        public SimaiChart? LastSimaiChart;
        public TrackInfo? LastTrackInfo;
        public float LastOffset = float.NaN;
        public int LastSignatureHash;

        public RenderCache()
        {
            TextFont = new SKFont(Typeface, 12);
            HanabiShader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(1, 0),
                [new SKColor(255, 0, 0, 100), new SKColor(255, 0, 0, 0)],
                SKShaderTileMode.Clamp);
            HanabiPaint.Shader = HanabiShader;

            CursorPath.MoveTo(-5, 0);
            CursorPath.LineTo(5, 0);
            CursorPath.LineTo(0, 8f);
            CursorPath.Close();
        }

        public void Dispose()
        {
            WavePath.Dispose();
            CursorPath.Dispose();
            HanabiPaint.Dispose();
            Paint.Dispose();
            HanabiShader.Dispose();
            TextFont.Dispose();
            Typeface.Dispose();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_renderCacheDisposed)
            return;

        _renderCache = new RenderCache();
        _renderCacheDisposed = false;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (!_renderCacheDisposed)
        {
            _renderCache.Dispose();
            _renderCacheDisposed = true;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void RequestNextAnimationFrame()
    {
        if (Interlocked.Exchange(ref _animationFramePending, 1) != 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            Volatile.Write(ref _animationFramePending, 0);
            InvalidateVisual();
        }, DispatcherPriority.Background);
    }

    class CustomDrawOp : ICustomDrawOperation
    {
        private readonly TrackInfo _trackInfo;
        private readonly SimaiChart? _simaiChart;
        private readonly List<(double, int, int)> _signatures;
        private readonly double _time;
        private readonly double _caretTime;
        private readonly float _zoomLevel;
        private readonly float _offset;
        private readonly bool _isAnimated;
        private readonly AnimationState _animationState;
        private readonly RenderCache _renderCache;
        private readonly Action _requestNextFrame;

        // Note colors
        static readonly SKColor WaveformColor = new(0, 100, 0, 150);
        static readonly SKColor BpmLineColor = SKColors.Yellow;
        static readonly SKColor TimingTickColor = SKColors.White;

        static readonly SKColor TapColor = SKColors.LightPink;
        static readonly SKColor TouchColor = SKColors.DeepSkyBlue;
        static readonly SKColor SlideHeadColor = SKColors.DeepSkyBlue;
        static readonly SKColor SlideBodyColor = SKColors.SkyBlue;

        static readonly SKColor BreakColor = SKColors.OrangeRed;
        static readonly SKColor EachColor = SKColors.Gold;
        static readonly SKColor MineColor = new(0x4F, 0x4F, 0x4F);
        static readonly SKColor MineBreakColor = new(0x83, 0x83, 0x83);
        static readonly SKColor MineSlideColor = new(0x4F, 0x4F, 0x4F);
        static readonly float[] DashIntervals = [4, 4];
        static readonly SKPathEffect DashEffect = SKPathEffect.CreateDash(DashIntervals, 0);

        // TouchHold layer colors
        static readonly SKColor TouchHoldLayer1 = new(0x00, 0xA5, 0xF7);
        static readonly SKColor TouchHoldLayer2 = new(0x16, 0xAC, 0x6E);
        static readonly SKColor TouchHoldLayer3 = new(0xF6, 0xEB, 0x00);
        static readonly SKColor TouchHoldLayer4 = new(0xF7, 0x46, 0x01);
        static readonly SKColor[] TouchHoldMineColors = [MineBreakColor, MineColor, MineBreakColor, MineColor];
        static readonly SKColor[] TouchHoldNormalColors = [TouchHoldLayer1, TouchHoldLayer2, TouchHoldLayer3, TouchHoldLayer4];

        static readonly SKColor CaretColor = new(200, 0, 0, 200);
        static readonly SKColor GhostCursorColor = SKColors.Orange;

        public CustomDrawOp(Rect bounds,
            TrackInfo trackInfo, double time, float zoomLevel, SimaiChart? simaiChart, List<(double, int, int)> signatures,
            float offset, double caretTime, bool isAnimated, AnimationState animationState,
            RenderCache renderCache, Action requestNextFrame)
        {
            _trackInfo = trackInfo;
            _time = time;
            _zoomLevel = zoomLevel;
            _simaiChart = simaiChart;
            _signatures = signatures;
            _offset = offset;
            _caretTime = caretTime;
            _isAnimated = isAnimated;
            _animationState = animationState;
            _renderCache = renderCache;
            _requestNextFrame = requestNextFrame;
            Bounds = bounds;
        }
        public void Dispose() { }
        public Rect Bounds { get; }
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation? other) => false;

        private static int LowerBound(IReadOnlyList<double> values, double target)
        {
            var low = 0;
            var high = values.Count;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                if (values[middle] < target)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private static int LowerBound(ReadOnlySpan<SimaiTiming> timings, double target)
        {
            var low = 0;
            var high = timings.Length;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                if (timings[middle].Time < target)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (_trackInfo is null) return;
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                Debug.WriteLine("SkiaSharp lease feature not available. Cannot render waveform.");
            else
            {
                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;
                var cache = _renderCache;
                var paint = cache.Paint;
                paint.Reset();
                paint.Style = SKPaintStyle.Fill;
                paint.Color = WaveformColor;
                canvas.Save();
                var width = Bounds.Width;
                var height = Bounds.Height;
                //Actuall Drawing here
                //make it smooth
                //TODO; Add Deltatime
                if (_isAnimated)
                {
                    _animationState.Time += 0.2 * (_time - _animationState.Time);
                    _animationState.Zoom += 0.2 * (_zoomLevel - _animationState.Zoom);
                }
                else
                {
                    _animationState.Time = _time;
                    _animationState.Zoom = _zoomLevel;
                }

                var needsNextFrame = _isAnimated &&
                    (Math.Abs(_time - _animationState.Time) > 0.005 ||
                     Math.Abs(_zoomLevel - _animationState.Zoom) > 0.005);
                if (needsNextFrame)
                    _requestNextFrame();

                var waveLevels = _trackInfo.RawWave;
                if (_animationState.Zoom > 3) waveLevels = _trackInfo.GetWaveThumbnails(2);
                if (_animationState.Zoom > 2) waveLevels = _trackInfo.GetWaveThumbnails(1);
                if (_animationState.Zoom > 1) waveLevels = _trackInfo.GetWaveThumbnails(0);
                var songLength = _trackInfo.Length;

                var currentTime = _animationState.Time;
                var step = songLength / waveLevels.Length;
                var deltatime = _animationState.Zoom;

                var startindex = (int)((currentTime - deltatime) / step);
                var stopindex = (int)((currentTime + deltatime) / step);
                var linewidth = (float)(width / (stopindex - startindex));
                var virtualStartIndex = (double)startindex;

                var wavePoints = cache.WavePoints;
                wavePoints.Clear();
                for (var i = startindex; i < stopindex; i++)
                {
                    if (i < 0) i = 0;
                    if (i >= waveLevels.Length - 1) break;

                    var x = (i - startindex) * linewidth;
                    var y = waveLevels[i] / 65535f * height + height / 2;

                    wavePoints.Add(new SKPoint((float)x, (float)y));
                }
                canvas.DrawPoints(SKPointMode.Polygon, wavePoints.ToArray(), paint);

                paint.IsAntialias = true;

                if (_simaiChart == null) return;

                //Draw Bpm Lines
                var bpmChangeTimes = cache.BpmChangeTimes;
                var bpmChangeValues = cache.BpmChangeValues;
                var strongBeats = cache.StrongBeats;
                var weakBeats = cache.WeakBeats;
                var signatureHash = new HashCode();
                foreach (var signature in _signatures)
                    signatureHash.Add(signature);
                var currentSignatureHash = signatureHash.ToHashCode();

                if (!ReferenceEquals(_simaiChart, cache.LastSimaiChart) ||
                    !ReferenceEquals(_trackInfo, cache.LastTrackInfo) ||
                    _offset != cache.LastOffset ||
                    currentSignatureHash != cache.LastSignatureHash)
                {
                    cache.LastSimaiChart = _simaiChart;
                    cache.LastTrackInfo = _trackInfo;
                    cache.LastOffset = _offset;
                    cache.LastSignatureHash = currentSignatureHash;
                    var lastbpm = -1f;
                    bpmChangeTimes.Clear();
                    bpmChangeValues.Clear();

                    //scan to get bpm change time and value
                    foreach (var timing in _simaiChart.Timings)
                    {
                        if (timing.Bpm != lastbpm)
                        {
                            bpmChangeTimes.Add(timing.Time + _offset);
                            bpmChangeValues.Add(timing.Bpm);
                            lastbpm = timing.Bpm;
                        }
                    }
                    bpmChangeTimes.Add(_trackInfo.Length);

                    double timeBeats = bpmChangeTimes.Count > 0 ? bpmChangeTimes[0] : 0;
                    var signatureNum = 4; // Time signature
                    var signatureDeno = 4; // Time signature
                    var currentBeat = 1;
                    double timePerBeat;
                    strongBeats.Clear();
                    weakBeats.Clear();

                    for (var i = 1; i < bpmChangeTimes.Count; i++)
                    {
                        while (timeBeats < bpmChangeTimes[i] - 0.05)
                        {
                            var sig = default((double, int, int));
                            for (var s = _signatures.Count - 1; s >= 0; s--)
                            {
                                if (timeBeats > _signatures[s].Item1 - 0.05)
                                {
                                    sig = _signatures[s];
                                    break;
                                }
                            }
                            if (sig != default)
                            {
                                signatureNum = sig.Item2;
                                signatureDeno = sig.Item3;
                            }

                            if (currentBeat > signatureNum) currentBeat = 1;
                            timePerBeat = 60.0 / bpmChangeValues[i - 1] * 4 / signatureDeno;

                            if (currentBeat == 1)
                                strongBeats.Add(timeBeats);
                            else
                                weakBeats.Add(timeBeats);

                            currentBeat++;
                            timeBeats += timePerBeat;
                        }
                        timeBeats = bpmChangeTimes[i];
                        currentBeat = 1;
                    }
                }

                double time = bpmChangeTimes.Count > 0 ? bpmChangeTimes[0] : 0;
                paint.Color = BpmLineColor;
                paint.StrokeWidth = 1;

                var visibleStartTime = currentTime - deltatime;
                var visibleEndTime = currentTime + deltatime;
                var firstBpmIndex = Math.Max(0, LowerBound(bpmChangeTimes, visibleStartTime) - 1);
                for (var i = firstBpmIndex; i < bpmChangeValues.Count; i++)
                {
                    time = bpmChangeTimes[i];
                    if (time > visibleEndTime) break;
                    var x = (float)((time / step - virtualStartIndex) * linewidth);
                    canvas.DrawText(bpmChangeValues[i].ToString(), x + 3f, 10, cache.TextFont, paint);
                }

                for (var i = LowerBound(strongBeats, visibleStartTime); i < strongBeats.Count; i++)
                {
                    var beatTime = strongBeats[i];
                    if (beatTime > visibleEndTime) break;
                    var x = (float)((beatTime / step - virtualStartIndex) * linewidth);
                    canvas.DrawLine(x, 0, x, (float)height, paint);
                }

                for (var i = LowerBound(weakBeats, visibleStartTime); i < weakBeats.Count; i++)
                {
                    var beatTime = weakBeats[i];
                    if (beatTime > visibleEndTime) break;
                    var x = (float)((beatTime / step - virtualStartIndex) * linewidth);
                    canvas.DrawLine(x, 0, x, 10, paint);
                }

                paint.Color = CaretColor;
                paint.StrokeWidth = 2;
                canvas.DrawLine((float)width / 2, 15, (float)width / 2, (float)height - 15, paint);

                paint.Style = SKPaintStyle.Stroke;
                // Draw timing white lines + notes
                var timings = _simaiChart.Timings;
                var firstNoteIndex = LowerBound(timings, visibleStartTime - _offset - 10.0);
                for (var noteIndex = firstNoteIndex; noteIndex < timings.Length; noteIndex++)
                {
                    var timing = timings[noteIndex];
                    time = timing.Time + _offset;
                    if (time > visibleEndTime) break;
                    var notes = timing.Notes;

                    //timing white line
                    {
                        paint.Color = TimingTickColor;
                        var xTick = (float)((time / step - virtualStartIndex) * linewidth);
                        canvas.DrawLine(xTick, (float)height - 10, xTick, (float)height, paint);
                    }

                    var x = (float)((time / step - virtualStartIndex) * linewidth);

                    foreach (var note in notes)
                    {
                        var seprate = (height - 30f) / 8f;
                        var y = (float)(note.StartPos * seprate + 10f);

                        if (note.IsHanabi)
                        {
                            var xDeltaHanabi = (float)(1f / step) * linewidth; // Hanabi is 1s due to frame analyze
                            var rectangleF = new SKRect(x, 0, x + xDeltaHanabi, (float)height);

                            if (note.Type == SimaiNoteType.TOUCHHOLD)
                                rectangleF.Left += (float)(note.Duration / step) * linewidth;

                            canvas.Save();
                            canvas.Translate(rectangleF.Left, rectangleF.Top);
                            canvas.Scale(Math.Max(rectangleF.Width, 0.0001f), 1);
                            canvas.DrawRect(
                                new SKRect(0, 0, 1, rectangleF.Height),
                                cache.HanabiPaint);
                            canvas.Restore();
                        }

                        switch (note.Type)
                        {
                            case SimaiNoteType.TAP:
                                paint.Color = note.IsMine ? (note.IsBreak ? MineBreakColor : MineColor) :
                                              note.IsBreak ? BreakColor :
                                              note.IsEach ? EachColor :
                                              TapColor;

                                if (note.IsStar)
                                {
                                    paint.StrokeWidth = 3;
                                    canvas.DrawText("*", x - 7f, y - 7f, cache.TextFont, paint);
                                }
                                else
                                {
                                    paint.StrokeWidth = 2;
                                    canvas.DrawOval(x, y, 3.5f, 3.5f, paint);
                                }
                                break;

                            case SimaiNoteType.TOUCH:
                                paint.StrokeWidth = 2;
                                paint.Color = note.IsMine ? (note.IsBreak ? MineBreakColor : MineColor) :
                                              note.IsEach ? EachColor : TouchColor;
                                canvas.DrawRect(x - 2.5f, y - 2.5f, 7, 7, paint);
                                break;

                            case SimaiNoteType.HOLD:
                                paint.StrokeWidth = 3.5f;
                                paint.Color = note.IsMine ? (note.IsBreak ? MineBreakColor : MineColor) :
                                              note.IsBreak ? BreakColor :
                                              note.IsEach ? EachColor :
                                              TapColor;

                                var xRight = (float)(x + (note.Duration / step) * linewidth);
                                if (!float.IsNormal(xRight)) xRight = ushort.MaxValue;
                                if (xRight - x < 1f) xRight = x + 5;
                                canvas.DrawLine(x, y, xRight, y, paint);
                                break;

                            case SimaiNoteType.TOUCHHOLD:
                                paint.StrokeWidth = 3.5f;
                                var xDelta = (float)(note.Duration / step) * linewidth / 4f;
                                if (!float.IsNormal(xDelta)) xDelta = ushort.MaxValue;
                                if (xDelta < 1f) xDelta = 1;

                                var touchHoldColors = note.IsMine ? TouchHoldMineColors : TouchHoldNormalColors;
                                for (var j = 0; j < 4; j++)
                                {
                                    paint.Color = touchHoldColors[j];
                                    canvas.DrawLine(x, y, x + xDelta * (4 - j), y, paint);
                                }
                                break;

                            case SimaiNoteType.SLIDE:
                                paint.StrokeWidth = 3.5f;
                                paint.Color = note.IsMine ? MineSlideColor :
                                              note.IsBreak ? BreakColor :
                                              note.IsEach ? EachColor :
                                              SlideBodyColor;
                                paint.PathEffect = DashEffect;
                                var xSlide = (float)((timing.Time + note.SlideShootDelay + _offset) / step - virtualStartIndex) * linewidth;
                                var xSlideRight = (float)(note.Duration / step) * linewidth + xSlide;

                                if (!float.IsNormal(xSlideRight)) xSlideRight = ushort.MaxValue;
                                if (!float.IsNormal(xSlide)) xSlide = ushort.MaxValue;

                                canvas.DrawLine(xSlide, y, xSlideRight, y, paint);
                                paint.PathEffect = null;
                                break;
                        }
                    }
                }

                time = _caretTime + _offset;
                if (time - currentTime <= deltatime)
                {
                    //Draw ghost cusor
                    paint.Color = GhostCursorColor;
                    paint.Style = SKPaintStyle.Fill;
                    var x2 = (float)(time / step - virtualStartIndex) * linewidth;
                    canvas.Save();
                    canvas.Translate(x2, 0);
                    canvas.DrawPath(cache.CursorPath, paint);
                    canvas.Restore();
                }

                canvas.Restore();
            }
        }
    }
    public override void Render(DrawingContext context)
    {
        if (TrackIf == null) return;

        context.Custom(new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height),
            TrackIf, Time, ZoomLevel, SimaiChart, Signatures ?? [], Offset, CaretTime,
            IsAnimated, _animationState, _renderCache, RequestNextAnimationFrame));
    }
}
