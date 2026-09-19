using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Cimai;
using MajdataEdit_Neo.Models;
using PropertyGenerator.Avalonia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace MajdataEdit_Neo.Controls;

internal partial class SimaiVisualizerControl : Control
{
    // Shared Skia resources — created once, reused for the lifetime of the process.
    private static readonly SKTypeface Typeface = SKTypeface.FromFamilyName(
        OperatingSystem.IsWindows() ? "Consolas" :
        OperatingSystem.IsMacOS() ? "Menlo" : "monospace",
        SKFontStyle.Bold);
    private static readonly SKFont TextFont = new(Typeface, 12);
    private static readonly SKPaint Paint = new();
    private static readonly SKPaint HanabiPaint = new()
    {
        Style = SKPaintStyle.Fill,
        Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(1, 0),
            [new SKColor(255, 0, 0, 100), new SKColor(255, 0, 0, 0)],
            SKShaderTileMode.Clamp),
    };
    private static readonly SKPath CursorPath = CreateCursorPath();
    private static SKPath CreateCursorPath()
    {
        var p = new SKPath();

        p.MoveTo(-5, 0);
        p.LineTo(5, 0);
        p.LineTo(0, 8f);
        p.Close();

        return p;
    }

    private static readonly SKPath StarPath = CreateStarPath();
    private static SKPath CreateStarPath()
    {
        var p = new SKPath();

        const float r = 4;
        const float r2 = r * 0.8660254f;
        const float r3 = r * 0.5f;

        p.MoveTo(0, -r);
        p.LineTo(0, r);

        p.MoveTo(-r2, -r3);
        p.LineTo(r2, r3);

        p.MoveTo(-r2, r3);
        p.LineTo(r2, -r3);

        return p;
    }

    private readonly AnimationState _animationState = new();
    private int _animationFramePending;

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial double Time { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial TrackInfo? TrackIf { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial float ZoomLevel { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial SimaiChart Chart { get; set; } = SimaiChart.Empty;

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial List<(double, int, int)>? Signatures { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial float Offset { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial double CaretTime { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial bool IsAnimated { get; set; }

    public SimaiVisualizerControl()
    {
        ClipToBounds = true;

        AffectsRender<SimaiVisualizerControl>(TimeProperty, TrackIfProperty, ZoomLevelProperty,
            ChartProperty, OffsetProperty, CaretTimeProperty, IsAnimatedProperty);
    }

    private sealed class AnimationState
    {
        public double Time;
        public double Zoom;
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

    public override void Render(DrawingContext context)
    {
        if (TrackIf is null) return;

        context.Custom(new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height),
            TrackIf, Time, ZoomLevel, Chart, Signatures ?? [], Offset, CaretTime,
            IsAnimated, _animationState, RequestNextAnimationFrame));
    }

    private sealed class CustomDrawOp(Rect bounds,
        TrackInfo trackInfo, double time, float zoomLevel,
        SimaiChart chart, List<(double, int, int)> signatures,
        float offset, double caretTime, bool isAnimated,
        AnimationState animationState, Action requestNextFrame) : ICustomDrawOperation
    {
        // Note colors
        private static readonly SKColor WaveformColor = new(0, 100, 0, 150);
        private static readonly SKColor BpmLineColor = SKColors.Yellow;
        private static readonly SKColor TimingTickColor = SKColors.White;
        private static readonly SKColor TapColor = SKColors.LightPink;
        private static readonly SKColor TouchColor = SKColors.DeepSkyBlue;
        private static readonly SKColor StarColor = SKColors.DeepSkyBlue;
        private static readonly SKColor SlideColor = SKColors.SkyBlue;
        private static readonly SKColor BreakColor = SKColors.OrangeRed;
        private static readonly SKColor EachColor = SKColors.Gold;
        private static readonly SKColor MineColor = new(0x4F, 0x4F, 0x4F);
        private static readonly SKColor MineBreakColor = new(0x83, 0x83, 0x83);
        private static readonly SKColor MineSlideColor = new(0x4F, 0x4F, 0x4F);
        private static readonly SKColor CaretColor = new(200, 0, 0, 200);
        private static readonly SKColor GhostCursorColor = SKColors.Orange;

        private static readonly float[] DashIntervals = [4, 4];
        private static readonly SKPathEffect DashEffect = SKPathEffect.CreateDash(DashIntervals, 0);

        // TouchHold layer colors
        private static readonly SKColor TouchHoldLayer1 = new(0x00, 0xA5, 0xF7);
        private static readonly SKColor TouchHoldLayer2 = new(0x16, 0xAC, 0x6E);
        private static readonly SKColor TouchHoldLayer3 = new(0xF6, 0xEB, 0x00);
        private static readonly SKColor TouchHoldLayer4 = new(0xF7, 0x46, 0x01);
        private static readonly SKColor[] TouchHoldMineColors = [MineBreakColor, MineColor, MineBreakColor, MineColor];
        private static readonly SKColor[] TouchHoldNormalColors = [TouchHoldLayer1, TouchHoldLayer2, TouchHoldLayer3, TouchHoldLayer4];
        private ReadOnlySpan<SimaiTiming> _timings => chart.IsDisposed ? default : chart.Timings;
        private readonly double _caretTime = caretTime;

        public Rect Bounds { get; } = bounds;

        public void Dispose() { }
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
            if (trackInfo is null) return;

            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                Debug.WriteLine("SkiaSharp lease feature not available. Cannot render waveform.");
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            Paint.Reset();
            Paint.Style = SKPaintStyle.Fill;
            Paint.Color = WaveformColor;
            canvas.Save();

            var width = Bounds.Width;
            var height = Bounds.Height;

            // Smooth animation interpolation.
            if (isAnimated)
            {
                animationState.Time += 0.2 * (time - animationState.Time);
                animationState.Zoom += 0.2 * (zoomLevel - animationState.Zoom);
            }
            else
            {
                animationState.Time = time;
                animationState.Zoom = zoomLevel;
            }

            if (isAnimated &&
                (Math.Abs(time - animationState.Time) > 0.005 ||
                 Math.Abs(zoomLevel - animationState.Zoom) > 0.005))
                requestNextFrame();

            // Pick zoom-level wave thumbnail.
            var waveLevels = trackInfo.RawWave;
            if (animationState.Zoom > 3) waveLevels = trackInfo.GetWaveThumbnails(2);
            else if (animationState.Zoom > 2) waveLevels = trackInfo.GetWaveThumbnails(1);
            else if (animationState.Zoom > 1) waveLevels = trackInfo.GetWaveThumbnails(0);

            var songLength = trackInfo.Length;
            var currentTime = animationState.Time;
            var step = songLength / waveLevels.Length;
            var deltatime = animationState.Zoom;

            // Draw waveform.
            var startIndex = (int)((currentTime - deltatime) / step);
            var stopIndex = (int)((currentTime + deltatime) / step);
            var linewidth = (float)(width / (stopIndex - startIndex));
            var virtualStartIndex = (double)startIndex;

            var wavePoints = new List<SKPoint>();
            for (var i = startIndex; i < stopIndex && i < waveLevels.Length - 1; i++)
            {
                if (i < 0) continue;
                var x = (i - startIndex) * linewidth;
                var y = waveLevels[i] / 65535f * height + height / 2;
                wavePoints.Add(new SKPoint((float)x, (float)y));
            }
            canvas.DrawPoints(SKPointMode.Polygon, wavePoints.ToArray(), Paint);

            Paint.IsAntialias = true;

            if (_timings.IsEmpty) return;

            // Recompute BPM changes and beats from scratch each frame.
            var bpmChangeTimes = new List<double>(32);
            var bpmChangeValues = new List<float>(32);
            var lastBpm = -1f;
            foreach (var timing in _timings)
            {
                if (timing.Bpm != lastBpm)
                {
                    bpmChangeTimes.Add(timing.Time + offset);
                    bpmChangeValues.Add(timing.Bpm);
                    lastBpm = timing.Bpm;
                }
            }
            bpmChangeTimes.Add(trackInfo.Length);

            var strongBeats = new List<double>(64);
            var weakBeats = new List<double>(128);
            var timeBeats = bpmChangeTimes.Count > 0 ? bpmChangeTimes[0] : 0;
            var signatureNum = 4;
            var signatureDeno = 4;
            var currentBeat = 1;
            for (var i = 1; i < bpmChangeTimes.Count; i++)
            {
                while (timeBeats < bpmChangeTimes[i] - 0.05)
                {
                    for (var s = signatures.Count - 1; s >= 0; s--)
                    {
                        if (timeBeats > signatures[s].Item1 - 0.05)
                        {
                            signatureNum = signatures[s].Item2;
                            signatureDeno = signatures[s].Item3;
                            break;
                        }
                    }

                    if (currentBeat > signatureNum) currentBeat = 1;
                    var timePerBeat = 60.0 / bpmChangeValues[i - 1] * 4 / signatureDeno;

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

            Paint.Color = BpmLineColor;
            Paint.StrokeWidth = 1;

            var visibleStartTime = currentTime - deltatime;
            var visibleEndTime = currentTime + deltatime;
            var firstBpmIndex = Math.Max(0, LowerBound(bpmChangeTimes, visibleStartTime) - 1);
            for (var i = firstBpmIndex; i < bpmChangeValues.Count; i++)
            {
                var t = bpmChangeTimes[i];
                if (t > visibleEndTime) break;
                var x = (float)((t / step - virtualStartIndex) * linewidth);
                canvas.DrawText(bpmChangeValues[i].ToString(), x + 3f, 10, TextFont, Paint);
            }

            foreach (var beatTime in strongBeats)
            {
                if (beatTime < visibleStartTime) continue;
                if (beatTime > visibleEndTime) break;
                var x = (float)((beatTime / step - virtualStartIndex) * linewidth);
                canvas.DrawLine(x, 0, x, (float)height, Paint);
            }

            foreach (var beatTime in weakBeats)
            {
                if (beatTime < visibleStartTime) continue;
                if (beatTime > visibleEndTime) break;
                var x = (float)((beatTime / step - virtualStartIndex) * linewidth);
                canvas.DrawLine(x, 0, x, 10, Paint);
            }

            // Caret line.
            Paint.Color = CaretColor;
            Paint.StrokeWidth = 2;
            canvas.DrawLine((float)width / 2, 15, (float)width / 2, (float)height - 15, Paint);

            Paint.Style = SKPaintStyle.Stroke;

            // Draw timing white lines + notes.
            var firstNoteIndex = LowerBound(_timings, visibleStartTime - offset - 10.0);
            for (var noteIndex = firstNoteIndex; noteIndex < _timings.Length; noteIndex++)
            {
                var timing = _timings[noteIndex];
                var t = timing.Time + offset;
                if (t > visibleEndTime) break;
                var notes = timing.Notes;

                Paint.Color = TimingTickColor;
                var xTick = (float)((t / step - virtualStartIndex) * linewidth);
                canvas.DrawLine(xTick, (float)height - 10, xTick, (float)height, Paint);

                var x = (float)((t / step - virtualStartIndex) * linewidth);
                var separate = (height - 30f) / 8f;

                foreach (var note in notes)
                {
                    var y = (float)(note.StartPos * separate + 10f);

                    if (note.IsHanabi)
                    {
                        var xDeltaHanabi = (float)(1f / step) * linewidth; // Hanabi is 1s due to frame analyze.
                        var rectF = new SKRect(x, 0, x + xDeltaHanabi, (float)height);
                        if (note.Type == SimaiNoteType.TOUCHHOLD)
                            rectF.Left += (float)(note.Duration / step) * linewidth;

                        canvas.Save();
                        canvas.Translate(rectF.Left, rectF.Top);
                        canvas.Scale(Math.Max(rectF.Width, 0.0001f), 1);
                        canvas.DrawRect(new SKRect(0, 0, 1, rectF.Height), HanabiPaint);
                        canvas.Restore();
                    }

                    switch (note.Type)
                    {
                        case SimaiNoteType.TAP:
                            Paint.Color = note.IsMine ? (note.IsBreak ? MineBreakColor : MineColor) :
                                          note.IsBreak ? BreakColor :
                                          note.IsEach ? EachColor :
                                          note.IsStar ? StarColor :
                                          TapColor;
                            if (note.IsStar)
                            {
                                Paint.StrokeWidth = 2;
                                var matrix = SKMatrix.CreateTranslation(x, y);
                                using var transformed = new SKPath();
                                StarPath.Transform(matrix, transformed);
                                canvas.DrawPath(transformed, Paint);
                            }
                            else
                            {
                                Paint.StrokeWidth = 2;
                                canvas.DrawOval(x, y, 3.5f, 3.5f, Paint);
                            }
                            break;

                        case SimaiNoteType.TOUCH:
                            Paint.StrokeWidth = 2;
                            Paint.Color = note.IsMine ? (note.IsBreak ? MineBreakColor : MineColor) :
                                          note.IsEach ? EachColor : TouchColor;
                            canvas.DrawRect(x - 2.5f, y - 2.5f, 7, 7, Paint);
                            break;

                        case SimaiNoteType.HOLD:
                            Paint.StrokeWidth = 3.5f;
                            Paint.Color = note.IsMine ? (note.IsBreak ? MineBreakColor : MineColor) :
                                          note.IsBreak ? BreakColor :
                                          note.IsEach ? EachColor :
                                          TapColor;
                            var xRight = (float)(x + (note.Duration / step) * linewidth);
                            if (!float.IsNormal(xRight)) xRight = ushort.MaxValue;
                            if (xRight - x < 1f) xRight = x + 5;
                            canvas.DrawLine(x, y, xRight, y, Paint);
                            break;

                        case SimaiNoteType.TOUCHHOLD:
                            Paint.StrokeWidth = 3.5f;
                            var xDelta = (float)(note.Duration / step) * linewidth / 4f;
                            if (!float.IsNormal(xDelta)) xDelta = ushort.MaxValue;
                            if (xDelta < 1f) xDelta = 1;
                            var touchHoldColors = note.IsMine ? TouchHoldMineColors : TouchHoldNormalColors;
                            for (var j = 0; j < 4; j++)
                            {
                                Paint.Color = touchHoldColors[j];
                                canvas.DrawLine(x, y, x + xDelta * (4 - j), y, Paint);
                            }
                            break;

                        case SimaiNoteType.SLIDE:
                            Paint.StrokeWidth = 3.5f;
                            Paint.Color = note.IsMine ? MineSlideColor :
                                          note.IsBreak ? BreakColor :
                                          note.IsEach ? EachColor :
                                          SlideColor;
                            Paint.PathEffect = DashEffect;
                            var xSlide = (float)((timing.Time + note.SlideShootDelay + offset) / step - virtualStartIndex) * linewidth;
                            var xSlideRight = (float)(note.Duration / step) * linewidth + xSlide;
                            if (!float.IsNormal(xSlideRight)) xSlideRight = ushort.MaxValue;
                            if (!float.IsNormal(xSlide)) xSlide = ushort.MaxValue;
                            canvas.DrawLine(xSlide, y, xSlideRight, y, Paint);
                            Paint.PathEffect = null;
                            break;
                    }
                }
            }

            // Ghost cursor.
            var caretTime = _caretTime + offset;
            if (caretTime - currentTime <= deltatime)
            {
                Paint.Color = GhostCursorColor;
                Paint.Style = SKPaintStyle.Fill;
                var x2 = (float)(caretTime / step - virtualStartIndex) * linewidth;
                canvas.Save();
                canvas.Translate(x2, 0);
                canvas.DrawPath(CursorPath, Paint);
                canvas.Restore();
            }

            canvas.Restore();
        }
    }
}