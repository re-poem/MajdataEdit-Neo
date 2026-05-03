using Avalonia.Media;
using AvaloniaEdit.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MajdataEdit_Neo.Types.SimaiAnalyzer;

public class SimaiTextMarker : TextSegment
{
    public Color Color { get; set; }
    public string? Message { get; set; }
    public SimaiTextMarker(int startOffset, int length)
    {
        this.StartOffset = startOffset;
        this.Length = length;
    }
}