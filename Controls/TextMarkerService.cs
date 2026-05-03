using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using MajdataEdit_Neo.Models.SimaiChecker;
using MajdataEdit_Neo.Types.SimaiAnalyzer;
using System.Collections.Generic;
using System.Linq;

namespace MajdataEdit_Neo.Controls;

public class TextMarkerService(TextDocument document, TextView textView) : IBackgroundRenderer
{
    private readonly TextSegmentCollection<SimaiTextMarker> _markers = new(document);
    private readonly TextView _textView = textView;

    public void UpdateDiags(IEnumerable<SimaiDiagnostic> diagnostics)
    {
        _markers.Clear();
        foreach (var d in diagnostics)
        {
            var marker = new SimaiTextMarker(d.PositionStart.Absolute, d.length);
            marker.Color = d.Severity switch
            {
                Severity.Error => Colors.Red,
                Severity.Warning => Colors.LightGreen,
                Severity.Info => Colors.LightBlue,
                _ => Colors.Transparent
            };
            marker.Message = d.Message + "\n\n" + d.Detail;

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
        if (_markers == null || textView.VisualLines.Count == 0) return;

        var visualLines = textView.VisualLines;
        foreach (var marker in _markers)
        {
            // 只绘制当前可见区域内的标记
            foreach (var line in visualLines)
            {
                if (line.StartOffset > marker.EndOffset || 
                    line.StartOffset + line.VisualLength < marker.StartOffset)
                    continue;

                // 获取标记在当前行的各个矩形区域（考虑跨行情况）
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
                {
                    DrawSquiggle(drawingContext, marker.Color, rect);
                }
            }
        }
    }

    private void DrawSquiggle(DrawingContext dc, Color color, Rect rect)
    {
        var pen = new Pen(new SolidColorBrush(color), 1.5);

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