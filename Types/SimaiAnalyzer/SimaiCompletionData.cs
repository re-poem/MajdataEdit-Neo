using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MajdataEdit_Neo.Types.SimaiAnalyzer;

public class SimaiCompletionData(string text, string? description) : ICompletionData
{
    public IImage Image => null!;
    public string Text { get; } = text;
    public object Content => Text;
    public object? Description { get; } = description;
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment,
                          EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}
