using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using MajdataEdit_Neo.Types.SimaiAnalyzer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MajdataEdit_Neo.Controls;

public class TextMarkerService(TextDocument document, TextView textView) : IBackgroundRenderer
{
    private readonly TextSegmentCollection<SimaiTextMarker> _markers = new(document);
    private readonly TextView _textView = textView;
    private static readonly Pen ErrorPen = new(new SolidColorBrush(Colors.Red), 1.5);
    private static readonly Pen WarningPen = new(new SolidColorBrush(Colors.LightGreen), 1.5);
    private static readonly Pen InfoPen = new(new SolidColorBrush(Colors.LightBlue), 1.5);
    private static readonly Pen TransparentPen = new(new SolidColorBrush(Colors.Transparent), 1.5);

    public void UpdateDiags(IEnumerable<SimaiDiagnostic> diagnostics)
    {
        _markers.Clear();
        foreach (var d in diagnostics)
        {
            var marker = new SimaiTextMarker(d.PositionStart.Absolute, d.Length)
            {
                Color = d.Severity switch
                {
                    Severity.Error => Colors.Red,
                    Severity.Warning => Colors.LightGreen,
                    Severity.Info => Colors.LightBlue,
                    _ => Colors.Transparent
                },
                Message = d.Message + "\n\n" + d.Detail
            };

            _markers.Add(marker);
        }
        _textView.Redraw();
    }

    // 渲染层级：选择 Layer.Selection 之后绘制，保证在文字下方
    public KnownLayer Layer => KnownLayer.Selection;

    public SimaiTextMarker? GetMarkerAtOffset(int offset)
    {
        return _markers.FindSegmentsContaining(offset).FirstOrDefault();
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView.VisualLines.Count == 0) return;

        var visualLines = textView.VisualLines;
        var firstLine = visualLines[0];
        var lastLine = visualLines[^1];
        var visibleStart = firstLine.StartOffset;
        var visibleEnd = lastLine.StartOffset + lastLine.VisualLength;
        foreach (var marker in _markers.FindOverlappingSegments(
                     visibleStart,
                     Math.Max(0, visibleEnd - visibleStart)))
        {
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
            {
                DrawSquiggle(drawingContext, GetPen(marker.Color), rect);
            }
        }
    }

    private static Pen GetPen(Color color)
    {
        if (color == Colors.Red) return ErrorPen;
        if (color == Colors.LightGreen) return WarningPen;
        if (color == Colors.LightBlue) return InfoPen;
        return TransparentPen;
    }

    private static void DrawSquiggle(DrawingContext dc, Pen pen, Rect rect)
    {
        double y = rect.Bottom - 1;
        double x = rect.Left;
        double endX = rect.Right;

        const double waveWidth = 4;
        const double waveHeight = 2;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x, y), false);

            while (x + waveWidth <= endX)
            {
                // 绘制一个波峰
                ctx.QuadraticBezierTo(
                    new Point(x + waveWidth / 4, y - waveHeight),
                    new Point(x + waveWidth / 2, y)
                );
                // 绘制一个波谷
                ctx.QuadraticBezierTo(
                    new Point(x + 3 * waveWidth / 4, y + waveHeight),
                    new Point(x + waveWidth, y)
                );
                x += waveWidth;
            }

            // 补齐最后不到一个周期的残余部分
            if (x < endX)
            {
                ctx.LineTo(new Point(endX, y));
            }
        }

        dc.DrawGeometry(null, pen, geometry);
    }
}
