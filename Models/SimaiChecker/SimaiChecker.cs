using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MajdataEdit_Neo.Models.SimaiChecker;

public static class SimaiChecker
{
    private static readonly char[] SlideTypeChars = ['-', '^', 'v', '<', '>', 'V', 'p', 'q', 's', 'z', 'w'];
    private static readonly string[] SlideTypeDoubleChars = ["pp", "qq"];
    private static readonly char[] TouchSensorTypes = ['A', 'B', 'C', 'D', 'E'];

    public static IReadOnlyList<SimaiDiagnostic> Check(string fumen)
    {
        var context = new CheckerContext(fumen);
        
        var (cleanedFumen, positionMap, newlines) = PreprocessNewlines(fumen, context);
        
        var segments = SplitIntoSegments(cleanedFumen, positionMap, context);

        for (var i = 0; i < segments.Count; i++)
        {
            CheckSegment(context, segments[i]);
        }

        CheckChartTermination(context, segments);
        return context.Diagnostics;
    }

    private static (string CleanedFumen, List<TextPosition> PositionMap, List<(int Index, TextPosition OriginalPos)> Newlines) 
        PreprocessNewlines(string fumen, CheckerContext context)
    {
        var cleanedChars = new List<char>();
        var positionMap = new List<TextPosition>();
        var newlines = new List<(int Index, TextPosition OriginalPos)>();
        
        var originalPos = TextPosition.Start;
        var inComment = false;
        
        for (var i = 0; i < fumen.Length; i++)
        {
            var c = fumen[i];
            
            if (inComment)
            {
                if (c == '\n')
                {
                    // 遇到换行符时自动结束注释
                    inComment = false;
                    newlines.Add((i, originalPos));
                    originalPos = originalPos.Advance(c);
                }
                else
                {
                    originalPos = originalPos.Advance(c);
                }
                continue;
            }
            
            if (c == '|' && i + 1 < fumen.Length && fumen[i + 1] == '|')
            {
                inComment = true;
                i++;
                originalPos = originalPos.Advance('|').Advance('|');
                continue;
            }
            
            if (c == '\n')
            {
                newlines.Add((i, originalPos));
                originalPos = originalPos.Advance(c);
                continue;
            }
            
            if (c == '\r')
            {
                originalPos = originalPos.Advance(c);
                continue;
            }
            
            cleanedChars.Add(c);
            positionMap.Add(originalPos);
            originalPos = originalPos.Advance(c);
        }
        
        var cleanedFumen = new string(cleanedChars.ToArray());
        
        CheckNewlinePositions(fumen, cleanedFumen, positionMap, newlines, context);
        
        return (cleanedFumen, positionMap, newlines);
    }

    private static void CheckNewlinePositions(
        string originalFumen, 
        string cleanedFumen, 
        List<TextPosition> positionMap,
        List<(int Index, TextPosition OriginalPos)> newlines,
        CheckerContext context)
    {
        foreach (var (newlineIndex, originalPos) in newlines)
        {
            var isValidPosition = IsNewlineAtValidPosition(originalFumen, newlineIndex);
            
            if (!isValidPosition)
            {
                context.AddWarning(
                    "Newline inside definition or note",
                    "Newlines should not appear inside BPM, HSpeed, Beat definitions, or note content. The newline will be ignored during parsing.",
                    originalPos,
                    2
                );
            }
        }
    }

    private static bool IsNewlineAtValidPosition(string fumen, int newlineIndex)
    {
        var beforeContext = GetContextBefore(fumen, newlineIndex);
        var afterContext = GetContextAfter(fumen, newlineIndex);
        
        if (IsInsideBpmDefinition(beforeContext, afterContext))
            return false;
        
        if (IsInsideHsDefinition(beforeContext, afterContext))
            return false;
        
        if (IsInsideBeatDefinition(beforeContext, afterContext))
            return false;
        
        if (IsInsideNoteContent(beforeContext, afterContext))
            return false;
        
        return true;
    }

    private static string GetContextBefore(string fumen, int index)
    {
        var start = Math.Max(0, index - 100);
        return fumen[start..index];
    }

    private static string GetContextAfter(string fumen, int index)
    {
        var end = Math.Min(fumen.Length, index + 100);
        return fumen[(index + 1)..end];
    }

    private static bool IsInsideBpmDefinition(string before, string after)
    {
        var lastOpenParen = before.LastIndexOf('(');
        if (lastOpenParen == -1) return false;
        
        var lastCloseParen = before.LastIndexOf(')');
        if (lastCloseParen != -1 && lastCloseParen > lastOpenParen) return false;
        
        var closeParenAfter = after.IndexOf(')');
        if (closeParenAfter == -1) return true;
        
        var openParenAfter = after.IndexOf('(');
        if (openParenAfter != -1 && openParenAfter < closeParenAfter) return false;
        
        return true;
    }

    private static bool IsInsideHsDefinition(string before, string after)
    {
        var lastHsStart = before.LastIndexOf("<HS*");
        if (lastHsStart == -1) return false;
        
        var afterHsStart = before[lastHsStart..];
        var lastCloseAngle = afterHsStart.LastIndexOf('>');
        if (lastCloseAngle != -1) return false;
        
        var closeAngleAfter = after.IndexOf('>');
        if (closeAngleAfter == -1) return true;
        
        return true;
    }

    private static bool IsInsideBeatDefinition(string before, string after)
    {
        var lastOpenBrace = before.LastIndexOf('{');
        if (lastOpenBrace == -1) return false;
        
        var lastCloseBrace = before.LastIndexOf('}');
        if (lastCloseBrace != -1 && lastCloseBrace > lastOpenBrace) return false;
        
        var closeBraceAfter = after.IndexOf('}');
        if (closeBraceAfter == -1) return true;
        
        var openBraceAfter = after.IndexOf('{');
        if (openBraceAfter != -1 && openBraceAfter < closeBraceAfter) return false;
        
        return true;
    }

    private static bool IsInsideNoteContent(string before, string after)
    {
        var lastComma = before.LastIndexOf(',');
        var lastCommaAfter = after.IndexOf(',');
        
        var afterTrimmed = after.TrimStart();
        var beforeTrimmed = before.TrimEnd();
        
        if (beforeTrimmed.Length == 0 || afterTrimmed.Length == 0)
            return false;
        
        if (afterTrimmed.StartsWith("(") || 
            afterTrimmed.StartsWith("{") || 
            afterTrimmed.StartsWith("<HS*") ||
            afterTrimmed.StartsWith("E") ||
            afterTrimmed.StartsWith("||"))
            return false;
        
        var lastCharBefore = beforeTrimmed[^1];
        var firstCharAfter = afterTrimmed[0];
        
        if (lastCharBefore == ',')
            return false;
        
        if (char.IsDigit(lastCharBefore) || IsTouchSensorType(lastCharBefore))
        {
            if (char.IsDigit(firstCharAfter) || 
                IsTouchSensorType(firstCharAfter) ||
                IsNoteModifier(firstCharAfter) ||
                IsSlideChar(firstCharAfter))
                return true;
        }
        
        if (IsNoteModifier(lastCharBefore) || lastCharBefore == ']' || lastCharBefore == ')')
        {
            if (char.IsDigit(firstCharAfter) || IsTouchSensorType(firstCharAfter))
                return true;
        }
        
        if (lastCharBefore == '[')
            return true;
        
        if (afterTrimmed.StartsWith(']'))
            return true;
        
        return false;
    }

    private static bool IsNoteModifier(char c)
    {
        return c switch
        {
            'h' or 'H' or 'b' or 'B' or 'x' or 'X' or 'm' or 'M' or 
            '$' or '@' or '?' or '!' or '*' or '/' or '`' or 'f' or 'F' => true,
            _ => false
        };
    }

    private static bool IsSlideChar(char c)
    {
        foreach (var slideChar in SlideTypeChars)
        {
            if (c == slideChar) return true;
        }
        return false;
    }

    private static bool IsTouchSensorType(char c)
    {
        var upper = char.ToUpperInvariant(c);
        foreach (var t in TouchSensorTypes)
        {
            if (upper == t) return true;
        }
        return false;
    }

    private static List<ChartSegment> SplitIntoSegments(string fumen, List<TextPosition> positionMap, CheckerContext context)
    {
        var segments = new List<ChartSegment>();
        var currentStart = 0;

        for (var i = 0; i < fumen.Length; i++)
        {
            var c = fumen[i];

            if (c == ',')
            {
                if (i > currentStart)
                {
                    var startPos = GetOriginalPosition(positionMap, currentStart);
                    segments.Add(new ChartSegment(fumen[currentStart..i], startPos, i - currentStart));
                }
                var commaPos = GetOriginalPosition(positionMap, i);
                segments.Add(new ChartSegment(",", commaPos, 1));
                currentStart = i + 1;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (i > currentStart)
                {
                    var startPos = GetOriginalPosition(positionMap, currentStart);
                    segments.Add(new ChartSegment(fumen[currentStart..i], startPos, i - currentStart));
                }
                currentStart = i + 1;
                continue;
            }
        }

        if (currentStart < fumen.Length)
        {
            var startPos = GetOriginalPosition(positionMap, currentStart);
            segments.Add(new ChartSegment(fumen[currentStart..], startPos, fumen.Length - currentStart));
        }

        return segments;
    }

    private static TextPosition GetOriginalPosition(List<TextPosition> positionMap, int cleanedIndex)
    {
        if (positionMap == null || positionMap.Count == 0)
            return TextPosition.Start;
            
        if (cleanedIndex >= positionMap.Count)
            cleanedIndex = positionMap.Count - 1;
        if (cleanedIndex < 0)
            cleanedIndex = 0;
            
        return positionMap[cleanedIndex];
    }

    private static void CheckSegment(CheckerContext context, ChartSegment segment)
    {
        if (string.IsNullOrWhiteSpace(segment.Content)) return;
        if (segment.Content == ",") return;

        var content = segment.Content;
        var startPos = segment.StartPosition;

        // 检查是否包含 E 后面跟着注释
        var eIndex = content.IndexOf('E');
        if (eIndex != -1)
        {
            // 检查 E 后面是否有注释
            var afterE = content[(eIndex + 1)..];
            var commentStart = afterE.IndexOf("||");
            if (commentStart != -1)
            {
                // 如果 E 后面跟着注释，只处理 E 部分
                return;
            }
            // 如果 E 是单独的字符，直接返回
            if (content == "E")
            {
                return;
            }
        }

        var noteStart = 0;
        var processedOriginalLength = 0;

        while (true)
        {
            var processedSomething = false;
            var checkingStartPos = startPos.Advance(segment.Content[..processedOriginalLength]);

            if (content.StartsWith("<HS*"))
            {
                var remaining = CheckHSpeedSyntax(context, content, checkingStartPos);
                processedOriginalLength += content.Length - remaining.Length;
                content = remaining;
                noteStart = processedOriginalLength;
                if (string.IsNullOrEmpty(content)) return;
                processedSomething = true;
            }
            else if (content.Contains("<HS*"))
            {
                var idx = content.IndexOf("<HS*");
                var hspeedEnd = content.IndexOf('>', idx);
                if (hspeedEnd != -1)
                {
                    CheckHSpeedSyntax(context, content[idx..], checkingStartPos.Advance(segment.Content[processedOriginalLength..idx]));
                    processedOriginalLength += hspeedEnd + 1;
                    content = content[(hspeedEnd + 1)..];
                    noteStart = processedOriginalLength;
                    processedSomething = true;
                }
            }

            if (content.StartsWith('('))
            {
                var bpmEnd = content.IndexOf(')');
                CheckBpmDefinition(context, content, checkingStartPos);
                context.HasBpmDefinition = true;
                if (bpmEnd == -1) return;
                processedOriginalLength += bpmEnd + 1;
                content = content[(bpmEnd + 1)..];
                noteStart = processedOriginalLength;
                processedSomething = true;
            }
            else if (content.Contains('('))
            {
                var idx = content.IndexOf('(');
                var bpmEnd = content.IndexOf(')', idx);
                if (bpmEnd != -1)
                {
                    CheckBpmDefinition(context, content[idx..], checkingStartPos.Advance(segment.Content[processedOriginalLength..idx]));
                    context.HasBpmDefinition = true;
                    processedOriginalLength += bpmEnd + 1;
                    content = content[(bpmEnd + 1)..];
                    noteStart = processedOriginalLength;
                    processedSomething = true;
                }
            }

            if (content.StartsWith('{'))
            {
                var beatEnd = content.IndexOf('}');
                if (!context.HasBpmDefinition)
                {
                    context.AddError(
                        "Beat definition without prior BPM",
                        "A BPM definition must appear before any beat definition in the chart",
                        checkingStartPos,
                        1
                    );
                }
                CheckBeatDefinition(context, content, checkingStartPos);
                if (beatEnd == -1) return;
                processedOriginalLength += beatEnd + 1;
                content = content[(beatEnd + 1)..];
                noteStart = processedOriginalLength;
                processedSomething = true;
            }
            else if (content.Contains('{'))
            {
                var idx = content.IndexOf('{');
                if (!context.HasBpmDefinition)
                {
                    context.AddError(
                        "Beat definition without prior BPM",
                        "A BPM definition must appear before any beat definition in the chart",
                        checkingStartPos.Advance(segment.Content[processedOriginalLength..idx]),
                        1
                    );
                }
                var beatEnd = content.IndexOf('}', idx);
                if (beatEnd != -1)
                {
                    CheckBeatDefinition(context, content[idx..], checkingStartPos.Advance(segment.Content[processedOriginalLength..idx]));
                    processedOriginalLength += beatEnd + 1;
                    content = content[(beatEnd + 1)..];
                    noteStart = processedOriginalLength;
                    processedSomething = true;
                }
            }

            if (!processedSomething) break;
        }

        if (string.IsNullOrEmpty(content)) return;

        var noteStartPos = startPos.Advance(segment.Content[..noteStart]);
        CheckNoteGroup(context, content, noteStartPos);
    }

    private static string CheckHSpeedSyntax(CheckerContext context, string content, TextPosition startPos)
    {
        var hspeedEnd = content.IndexOf('>');
        if (hspeedEnd == -1)
        {
            context.AddError(
                "HSpeed definition not closed",
                "HSpeed must be enclosed in angle brackets, e.g., <HS*1.5>",
                startPos,
                1
            );
            return content;
        }

        var hspeedContent = content[4..hspeedEnd];
        if (string.IsNullOrEmpty(hspeedContent))
        {
            context.AddError(
                "Empty HSpeed value",
                "HSpeed value cannot be empty",
                startPos,
                4
            );
            return content[(hspeedEnd + 1)..];
        }

        if (!double.TryParse(hspeedContent, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            context.AddError(
                $"Invalid HSpeed value: '{hspeedContent}'",
                "HSpeed must be a number",
                startPos.Advance("<HS*"),
                hspeedContent.Length
            );
        }

        return content[(hspeedEnd + 1)..];
    }

    private static void CheckBpmDefinition(CheckerContext context, string content, TextPosition startPos)
    {
        var closeIndex = content.IndexOf(')');
        if (closeIndex == -1)
        {
            context.AddError(
                "BPM definition not closed",
                "BPM must be enclosed in parentheses, e.g., (120)",
                startPos,
                1
            );
            return;
        }

        var bpmContent = content[1..closeIndex];
        if (string.IsNullOrEmpty(bpmContent))
        {
            context.AddError(
                "Empty BPM definition",
                "BPM value cannot be empty",
                startPos,
                1
            );
            return;
        }

        if (!double.TryParse(bpmContent, NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm) || bpm <= 0)
        {
            context.AddError(
                $"Invalid BPM value: '{bpmContent}'",
                "BPM must be a positive number",
                startPos.Advance("("),
                bpmContent.Length
            );
        }
    }

    private static void CheckBeatDefinition(CheckerContext context, string content, TextPosition startPos)
    {
        var closeIndex = content.IndexOf('}');
        if (closeIndex == -1)
        {
            context.AddError(
                "Beat definition not closed",
                "Beat must be enclosed in braces, e.g., {4} or {#0.5}",
                startPos,
                1
            );
            return;
        }

        var beatContent = content[1..closeIndex];
        if (string.IsNullOrEmpty(beatContent))
        {
            context.AddError(
                "Empty beat definition",
                "Beat value cannot be empty",
                startPos,
                1
            );
            return;
        }

        if (beatContent.StartsWith('#'))
        {
            var timeValue = beatContent[1..];
            if (!double.TryParse(timeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var time) || time <= 0)
            {
                context.AddError(
                    $"Invalid absolute time value: '{timeValue}'",
                    "Absolute time must be a positive number (in seconds)",
                    startPos.Advance("{#"),
                    timeValue.Length
                );
            }
        }
        else
        {
            if (!int.TryParse(beatContent, out var beat) || beat <= 0)
            {
                context.AddError(
                    $"Invalid beat value: '{beatContent}'",
                    "Beat must be a positive integer, e.g., {4}, {8}, {16}",
                    startPos.Advance("{"),
                    beatContent.Length
                );
            }
        }
    }

    private static void CheckNoteGroup(CheckerContext context, string content, TextPosition startPos)
    {
        var notes = SplitByEach(content);

        foreach (var note in notes)
        {
            if (string.IsNullOrEmpty(note.Content))
            {
                context.AddError(
                    "Empty note in EACH group",
                    "EACH groups cannot contain empty notes",
                    startPos.Advance(content[..note.StartIndex]),
                    1
                );
                continue;
            }
            CheckSingleNote(context, note.Content, startPos.Advance(content[..note.StartIndex]));
        }
    }

    private static List<(string Content, int StartIndex)> SplitByEach(string content)
    {
        var result = new List<(string, int)>();
        var currentStart = 0;

        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == '/' || content[i] == '`')
            {
                if (i > currentStart)
                    result.Add((content[currentStart..i], currentStart));
                currentStart = i + 1;
            }
        }

        if (currentStart < content.Length)
            result.Add((content[currentStart..], currentStart));

        return result;
    }

    private static void CheckSingleNote(CheckerContext context, string content, TextPosition startPos)
    {
        if (string.IsNullOrEmpty(content)) return;

        if (IsTouchNote(content, out var sensorType, out var sensorIndex))
        {
            CheckTouchNote(context, content, startPos, sensorType, sensorIndex);
            return;
        }

        if (char.IsDigit(content[0]))
        {
            CheckButtonNote(context, content, startPos);
            return;
        }

        context.AddError(
            $"Invalid note: '{content}'",
            "Note must start with a button number (1-8) or sensor type (A-E)",
            startPos,
            content.Length
        );
    }

    private static bool IsTouchNote(string content, out char sensorType, out int? sensorIndex)
    {
        sensorType = '\0';
        sensorIndex = null;

        if (string.IsNullOrEmpty(content)) return false;

        var c = char.ToUpperInvariant(content[0]);
        if (!IsTouchSensorType(c)) return false;

        sensorType = c;

        if (content.Length == 1)
        {
            return sensorType == 'C';
        }

        var idx = 1;
        if (content.Length > idx && char.IsDigit(content[idx]))
        {
            sensorIndex = content[idx] - '0';
            idx++;
        }

        return true;
    }

    private static void CheckTouchNote(CheckerContext context, string content, TextPosition startPos, char sensorType, int? sensorIndex)
    {
        if (sensorType == 'C')
        {
            if (sensorIndex.HasValue && sensorIndex.Value != 1 && sensorIndex.Value != 2)
            {
                context.AddError(
                    $"Invalid C sensor index: {sensorIndex.Value}",
                    "C sensor can only have index 1 or 2 (or no index)",
                    startPos,
                    2
                );
            }
        }
        else
        {
            if (!sensorIndex.HasValue || sensorIndex.Value < 1 || sensorIndex.Value > 8)
            {
                context.AddError(
                    $"Invalid sensor index for {sensorType}",
                    "Sensor index must be between 1 and 8",
                    startPos,
                    1
                );
            }
        }

        var idx = 1;
        if (sensorIndex.HasValue) idx++;

        var isHold = false;
        var durationStart = -1;
        var durationEnd = -1;

        for (var i = idx; i < content.Length; i++)
        {
            var c = char.ToLowerInvariant(content[i]);

            if (c == '[')
            {
                if (durationStart != -1)
                {
                    context.AddError(
                        "Duplicate duration bracket",
                        "Touch note can only have one duration specification",
                        startPos.Advance(content[..i]),
                        1
                    );
                }
                durationStart = i;
                var closeIdx = content.IndexOf(']', i);
                if (closeIdx == -1)
                {
                    context.AddError(
                        "Duration not closed for touch hold",
                        "Duration must be enclosed in brackets, e.g., Ch[4:3]",
                        startPos.Advance(content[..i]),
                        1
                    );
                    return;
                }
                durationEnd = closeIdx;
                i = closeIdx;
                continue;
            }

            switch (c)
            {
                case 'h':
                    isHold = true;
                    break;
                case 'f':
                case 'x':
                case 'b':
                case 'm':
                    break;
                default:
                    context.AddError(
                        $"Invalid character in touch note: '{content[i]}'",
                        "Touch notes can only contain 'f' (firework), 'h' (hold), 'x' (EX), 'b' (break), 'm' (mine) modifiers",
                        startPos.Advance(content[..i]),
                        1
                    );
                    break;
            }
        }

        if (isHold && durationStart != -1)
        {
            var duration = content[(durationStart + 1)..durationEnd];
            ValidateDuration(context, content, startPos, duration, durationStart, "TOUCH HOLD", allowSlideFormat: false);
        }
        else if (durationStart != -1 && !isHold)
        {
            context.AddWarning(
                "Duration specified for non-hold touch note",
                "Duration is only meaningful for touch hold notes",
                startPos.Advance(content[..durationStart]),
                durationEnd - durationStart + 1
            );
        }
    }

    private static void CheckButtonNote(CheckerContext context, string content, TextPosition startPos)
    {
        var firstDigit = content[0] - '0';
        if (firstDigit < 1 || firstDigit > 8)
        {
            context.AddError(
                $"Invalid button position: {firstDigit}",
                "Button position must be between 1 and 8",
                startPos,
                1
            );
            return;
        }

        if (content.Length == 1) return;

        if (char.IsDigit(content[1]) && (content.Length == 2 || !IsSlideChar(content[1])))
        {
            var secondDigit = content[1] - '0';
            if (secondDigit < 1 || secondDigit > 8)
            {
                context.AddError(
                    $"Invalid button position: {secondDigit}",
                    "Button position must be between 1 and 8",
                    startPos.Advance(content[0].ToString()),
                    1
                );
            }
            return;
        }

        var noteInfo = ParseNoteInfo(content);
        ValidateNoteInfo(context, content, startPos, noteInfo);
    }

    private static NoteInfo ParseNoteInfo(string content)
    {
        var info = new NoteInfo
        {
            StartPosition = content[0] - '0'
        };

        var idx = 1;
        var lastSlideEndPosition = info.StartPosition;

        while (idx < content.Length)
        {
            var c = content[idx];

            switch (char.ToLowerInvariant(c))
            {
                case 'h':
                    info.IsHold = true;
                    idx++;
                    break;
                case 'b':
                    if (info.Slides.Count > 0)
                    {
                        var lastSlide = info.Slides[^1];
                        if (idx + 1 < content.Length && content[idx + 1] == '[')
                        {
                            lastSlide.IsBreak = true;
                        }
                        else if (idx == content.Length - 1)
                        {
                            lastSlide.IsBreak = true;
                        }
                        else
                        {
                            info.IsBreak = true;
                        }
                    }
                    else
                    {
                        info.IsBreak = true;
                    }
                    idx++;
                    break;
                case 'x':
                    info.IsEx = true;
                    idx++;
                    break;
                case 'm':
                    if (info.Slides.Count > 0)
                    {
                        var lastSlide = info.Slides[^1];
                        if (idx + 1 < content.Length && content[idx + 1] == '[')
                        {
                            lastSlide.IsMine = true;
                        }
                        else if (idx == content.Length - 1)
                        {
                            lastSlide.IsMine = true;
                        }
                        else
                        {
                            info.IsMine = true;
                        }
                    }
                    else
                    {
                        info.IsMine = true;
                    }
                    idx++;
                    break;
                case '$':
                    info.HasStar = true;
                    if (idx + 1 < content.Length && content[idx + 1] == '$')
                    {
                        info.HasDoubleStar = true;
                        idx += 2;
                    }
                    else
                    {
                        idx++;
                    }
                    break;
                case '@':
                    info.NoStar = true;
                    idx++;
                    break;
                case '?':
                    info.FadeSlide = true;
                    idx++;
                    break;
                case '!':
                    info.NoFadeSlide = true;
                    idx++;
                    break;
                case '[':
                    var closeIdx = content.IndexOf(']', idx);
                    if (closeIdx != -1)
                    {
                        if (info.Slides.Count > 0)
                        {
                            var lastSlide = info.Slides[^1];
                            lastSlide.Duration = content[(idx + 1)..closeIdx];
                            lastSlide.DurationStart = idx;
                            lastSlide.DurationEnd = closeIdx;
                        }
                        else
                        {
                            info.Duration = content[(idx + 1)..closeIdx];
                            info.DurationStart = idx;
                            info.DurationEnd = closeIdx;
                        }
                        idx = closeIdx + 1;
                    }
                    else
                    {
                        if (info.Slides.Count > 0)
                        {
                            var lastSlide = info.Slides[^1];
                            lastSlide.Duration = content[(idx + 1)..];
                            lastSlide.DurationStart = idx;
                            info.DurationEnd = content.Length - 1;
                        }
                        else
                        {
                            info.Duration = content[(idx + 1)..];
                            info.DurationStart = idx;
                            info.DurationEnd = content.Length - 1;
                        }
                        idx = content.Length;
                    }
                    break;
                case '*':
                    info.HasSameStartPointSlides = true;
                    idx++;
                    lastSlideEndPosition = info.StartPosition;
                    info.NextSlideIsSameHeadChainStart = true;
                    break;
                default:
                    var slideMatch = TryMatchSlide(content, idx, lastSlideEndPosition);
                    if (slideMatch != null)
                    {
                        if (info.NextSlideIsSameHeadChainStart)
                        {
                            slideMatch.IsSameHeadChainStart = true;
                            info.NextSlideIsSameHeadChainStart = false;
                        }
                        info.Slides.Add(slideMatch);
                        idx = slideMatch.EndIndex;
                        if (slideMatch.EndPosition.HasValue)
                        {
                            lastSlideEndPosition = slideMatch.EndPosition.Value;
                        }
                    }
                    else
                    {
                        info.UnknownChars.Add((c, idx));
                        idx++;
                    }
                    break;
            }
        }

        return info;
    }

    private static SlideInfo? TryMatchSlide(string content, int startIdx, int noteStartPosition)
    {
        var idx = startIdx;
        var slide = new SlideInfo { StartIndex = idx, StartPosition = noteStartPosition };

        foreach (var doubleChar in SlideTypeDoubleChars)
        {
            if (idx + 2 <= content.Length && content[idx..(idx + 2)] == doubleChar)
            {
                slide.SlideType = doubleChar;
                idx += 2;
                break;
            }
        }

        if (slide.SlideType == null)
        {
            foreach (var slideChar in SlideTypeChars)
            {
                if (idx < content.Length && content[idx] == slideChar)
                {
                    slide.SlideType = slideChar.ToString();
                    idx++;
                    break;
                }
            }
        }

        if (slide.SlideType == null) return null;

        if (slide.SlideType == "V")
        {
            if (idx < content.Length && char.IsDigit(content[idx]))
            {
                slide.FlexionPoint = content[idx] - '0';
                idx++;
            }
        }

        if (idx < content.Length && char.IsDigit(content[idx]))
        {
            slide.EndPosition = content[idx] - '0';
            idx++;
        }

        if (idx < content.Length && content[idx] == '[')
        {
            var closeIdx = content.IndexOf(']', idx);
            if (closeIdx != -1)
            {
                slide.Duration = content[(idx + 1)..closeIdx];
                slide.DurationStart = idx;
                slide.DurationEnd = closeIdx;
                idx = closeIdx + 1;
            }
        }

        if (idx < content.Length && char.ToLowerInvariant(content[idx]) == 'b')
        {
            slide.IsBreak = true;
            idx++;
        }

        if (idx < content.Length && char.ToLowerInvariant(content[idx]) == 'm')
        {
            slide.IsMine = true;
            idx++;
        }

        slide.EndIndex = idx;
        return slide;
    }

    private static void ValidateNoteInfo(CheckerContext context, string content, TextPosition startPos, NoteInfo info)
    {
        foreach (var (c, idx) in info.UnknownChars)
        {
            context.AddError(
                $"Unknown character in note: '{c}'",
                $"Character '{c}' is not a valid note modifier or slide type",
                startPos.Advance(content[..idx]),
                content.Length
            );
        }

        if (info.IsHold && info.Slides.Count > 0)
        {
            context.AddError(
                "Note cannot be both HOLD and SLIDE",
                "A note can only be one type: TAP, HOLD, or SLIDE",
                startPos,
                content.Length
            );
        }

        if (info.HasStar && info.NoStar)
        {
            context.AddWarning(
                "Conflicting star modifiers: '$' and '@'",
                "Using both '$' (force star) and '@' (no star) is contradictory",
                startPos,
                content.Length
            );
        }

        if (info.FadeSlide && info.NoFadeSlide)
        {
            context.AddWarning(
                "Conflicting slide fade modifiers: '?' and '!'",
                "Using both '?' (fade in) and '!' (no fade) is contradictory",
                startPos,
                content.Length
            );
        }

        if (info.HasStar && info.Slides.Count > 0)
        {
            context.AddWarning(
                "Redundant star modifier '$' on SLIDE",
                "SLIDE notes automatically have a star shape; '$' is redundant here",
                startPos,
                content.Length
            );
        }

        if (info.NoStar && info.Slides.Count == 0)
        {
            context.AddWarning(
                "Invalid '@' modifier on non-SLIDE note",
                "The '@' modifier (no star) is only meaningful for SLIDE notes",
                startPos,
                content.Length
            );
        }

        if (info.FadeSlide && info.Slides.Count == 0)
        {
            context.AddWarning(
                "Invalid '?' modifier on non-SLIDE note",
                "The '?' modifier (fade slide) is only meaningful for SLIDE notes",
                startPos,
                content.Length
            );
        }

        if (info.NoFadeSlide && info.Slides.Count == 0)
        {
            context.AddWarning(
                "Invalid '!' modifier on non-SLIDE note",
                "The '!' modifier (no fade slide) is only meaningful for SLIDE notes",
                startPos,
                content.Length
            );
        }

        if (info.IsHold && info.Duration == null)
        {
            context.AddInfo(
                "HOLD note missing duration",
                "HOLD notes need a duration specified. When you want a short hold, it is better to explicitly mark [1:0] or [384:1]",
                startPos.Advance(content),
                1
            );
        }

        if (info.IsHold && info.Duration != null)
        {
            ValidateDuration(context, content, startPos, info.Duration, info.DurationStart, "HOLD", allowSlideFormat: false);
        }

        ValidateSlidesDuration(context, content, startPos, info);

        if (!info.IsHold && info.Slides.Count == 0 && info.Duration != null)
        {
            context.AddWarning(
                "Duration specified for non-HOLD/SLIDE note",
                "Duration is only meaningful for HOLD and SLIDE notes",
                startPos.Advance(content[..info.DurationStart]),
                info.Duration.Length
            );
        }
    }

    private static void ValidateSlidesDuration(CheckerContext context, string content, TextPosition startPos, NoteInfo info)
    {
        if (info.Slides.Count == 0) return;

        if (info.HasSameStartPointSlides)
        {
            var chains = SplitIntoSlideChains(info.Slides);
            foreach (var chain in chains)
            {
                ValidateSlideChain(context, content, startPos, chain);
            }
        }
        else
        {
            ValidateSlideChain(context, content, startPos, info.Slides);
        }
    }

    private static List<List<SlideInfo>> SplitIntoSlideChains(List<SlideInfo> slides)
    {
        var chains = new List<List<SlideInfo>>();
        var currentChain = new List<SlideInfo>();

        foreach (var slide in slides)
        {
            if (slide.IsSameHeadChainStart && currentChain.Count > 0)
            {
                chains.Add(currentChain);
                currentChain = new List<SlideInfo>();
            }
            currentChain.Add(slide);
        }

        if (currentChain.Count > 0)
        {
            chains.Add(currentChain);
        }

        return chains;
    }

    private static void ValidateSlideChain(CheckerContext context, string content, TextPosition startPos, List<SlideInfo> chain)
    {
        if (chain.Count == 0) return;

        var slidesWithDuration = chain.Count(s => s.Duration != null);
        var lastSlide = chain[^1];

        foreach (var slide in chain)
        {
            ValidateSlide(context, content, startPos, slide, checkDuration: false);
        }

        if (slidesWithDuration == 0)
        {
            context.AddError(
                "Slide missing duration",
                "Slide must have a duration specified, e.g., [8:1] or [#1.5]",
                startPos.Advance(content[..lastSlide.EndIndex]),
                1
            );
            return;
        }

        if (slidesWithDuration == chain.Count)
        {
            foreach (var slide in chain)
            {
                if (slide.Duration != null)
                {
                    ValidateDuration(context, content, startPos, slide.Duration, slide.DurationStart, "SLIDE", allowSlideFormat: true);
                }
            }
            return;
        }

        if (slidesWithDuration == 1 && lastSlide.Duration != null)
        {
            ValidateDuration(context, content, startPos, lastSlide.Duration, lastSlide.DurationStart, "SLIDE", allowSlideFormat: true);
            return;
        }

        context.AddError(
            "Invalid slide duration specification",
            "For connected slides, either all slides must have individual durations, or only the last slide can have a duration (applied to entire chain)",
            startPos,
            content.Length
        );
    }

    private static void ValidateDuration(CheckerContext context, string content, TextPosition startPos,
        string duration, int durationStart, string noteType, bool allowSlideFormat)
    {
        if (string.IsNullOrEmpty(duration))
        {
            context.AddError(
                $"Empty duration for {noteType}",
                "Duration cannot be empty",
                startPos.Advance(content[..durationStart]),
                2
            );
            return;
        }

        var hashCount = CountChar(duration, '#');
        var colonCount = CountChar(duration, ':');

        if (allowSlideFormat && hashCount >= 2)
        {
            ValidateSlideDuration(context, content, startPos, duration, durationStart);
            return;
        }

        if (hashCount == 0 && colonCount == 0)
        {
            if (!double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) || val <= 0)
            {
                context.AddError(
                    $"Invalid duration: '{duration}'",
                    "Duration must be a positive number or use format like '8:1' or '#1.5'",
                    startPos.Advance(content[..(durationStart + 1)]),
                    duration.Length
                );
            }
        }
        else if (hashCount == 0 && colonCount == 1)
        {
            ValidateRatioDuration(context, content, startPos, duration, durationStart);
        }
        else if (hashCount == 1 && duration.StartsWith('#'))
        {
            var timeValue = duration[1..];
            if (!double.TryParse(timeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var time) || time <= 0)
            {
                context.AddError(
                    $"Invalid absolute time: '{timeValue}'",
                    "Absolute time must be a positive number (in seconds)",
                    startPos.Advance(content[..(durationStart + 2)]),
                    timeValue.Length
                );
            }
        }
        else if (hashCount == 1 && !duration.StartsWith('#'))
        {
            ValidateCustomBpmDuration(context, content, startPos, duration, durationStart);
        }
        else
        {
            context.AddError(
                $"Invalid duration format: '{duration}'",
                "Duration format is invalid. Use 'division:beats', '#seconds', or 'BPM#division:beats'",
                startPos.Advance(content[..(durationStart + 1)]),
                duration.Length
            );
        }
    }

    private static void ValidateSlideDuration(CheckerContext context, string content, TextPosition startPos,
        string duration, int durationStart)
    {
        var parts = SplitByChar(duration, '#');

        if (parts.Count < 3)
        {
            context.AddError(
                $"Invalid slide duration format: '{duration}'",
                "Slide duration with '##' should be 'startTime##moveTime'",
                startPos.Advance(content[..(durationStart + 1)]),
                duration.Length
            );
            return;
        }

        var startTimeStr = parts[0];
        if (!string.IsNullOrEmpty(startTimeStr))
        {
            if (!double.TryParse(startTimeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var startTime) || startTime < 0)
            {
                context.AddError(
                    $"Invalid slide start time: '{startTimeStr}'",
                    "Slide start time must be a non-negative number (in seconds)",
                    startPos.Advance(content[..(durationStart + 1)]),
                    startTimeStr.Length
                );
            }
        }

        var moveTimeStr = parts[^1];
        var moveTimeOffset = durationStart + 1 + duration.LastIndexOf('#') + 1;

        if (string.IsNullOrEmpty(moveTimeStr))
        {
            context.AddError(
                "Empty slide move time",
                "Slide move time cannot be empty",
                startPos.Advance(content[..moveTimeOffset]),
                1
            );
            return;
        }

        if (moveTimeStr.Contains(':'))
        {
            ValidateRatioDuration(context, content, startPos, moveTimeStr, moveTimeOffset - 1);
        }
        else if (!double.TryParse(moveTimeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            context.AddError(
                $"Invalid slide move time: '{moveTimeStr}'",
                "Slide move time must be a number or ratio format like '8:1'",
                startPos.Advance(content[..moveTimeOffset]),
                moveTimeStr.Length
            );
        }
    }

    private static void ValidateRatioDuration(CheckerContext context, string content, TextPosition startPos,
        string duration, int durationStart)
    {
        var colonIdx = duration.IndexOf(':');
        if (colonIdx <= 0 || colonIdx == duration.Length - 1)
        {
            context.AddError(
                $"Invalid duration format: '{duration}'",
                "Duration format should be 'division:beats', e.g., '4:2' means 2 beats at quarter note division",
                startPos.Advance(content[..(durationStart + 1)]),
                duration.Length
            );
            return;
        }

        var divisionStr = duration[..colonIdx];
        var beatsStr = duration[(colonIdx + 1)..];

        if (!int.TryParse(divisionStr, out var division) || division <= 0)
        {
            context.AddError(
                $"Invalid division: '{divisionStr}'",
                "Division must be a positive integer (e.g., 4 for quarter note, 8 for eighth note)",
                startPos.Advance(content[..(durationStart + 1)]),
                divisionStr.Length
            );
        }

        if (!int.TryParse(beatsStr, out var beats) || beats < 0)
        {
            context.AddError(
                $"Invalid beat count: '{beatsStr}'",
                "Beat count must be a non-negative integer",
                startPos.Advance(content[..(durationStart + 1 + colonIdx + 1)]),
                beatsStr.Length
            );
        }
    }

    private static void ValidateCustomBpmDuration(CheckerContext context, string content, TextPosition startPos,
        string duration, int durationStart)
    {
        var hashIdx = duration.IndexOf('#');
        var bpmStr = duration[..hashIdx];
        var restStr = duration[(hashIdx + 1)..];

        if (string.IsNullOrEmpty(bpmStr))
        {
            context.AddError(
                "Empty BPM in duration",
                "Custom BPM cannot be empty",
                startPos.Advance(content[..(durationStart + 1)]),
                1
            );
            return;
        }

        if (!double.TryParse(bpmStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm) || bpm <= 0)
        {
            context.AddError(
                $"Invalid BPM: '{bpmStr}'",
                "BPM must be a positive number",
                startPos.Advance(content[..(durationStart + 1)]),
                bpmStr.Length
            );
            return;
        }

        if (string.IsNullOrEmpty(restStr))
        {
            context.AddError(
                "Empty duration after BPM",
                "Duration must be specified after BPM",
                startPos.Advance(content[..(durationStart + 1 + hashIdx + 1)]),
                1
            );
            return;
        }

        if (restStr.Contains(':'))
        {
            ValidateRatioDuration(context, content, startPos, restStr, durationStart + 1 + hashIdx);
        }
        else if (!double.TryParse(restStr, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            context.AddError(
                $"Invalid duration: '{restStr}'",
                "Duration must be a number or ratio format like '8:1'",
                startPos.Advance(content[..(durationStart + 1 + hashIdx + 1)]),
                restStr.Length
            );
        }
    }

    private static void ValidateSlide(CheckerContext context, string content, TextPosition startPos,
        SlideInfo slide, bool checkDuration)
    {
        if (slide.EndPosition == null)
        {
            context.AddError(
                $"Slide missing end position",
                $"Slide type '{slide.SlideType}' requires an end position (button 1-8)",
                startPos.Advance(content[..slide.StartIndex]),
                content.Length
            );
            return;
        }

        if (slide.EndPosition < 1 || slide.EndPosition > 8)
        {
            context.AddError(
                $"Invalid slide end position: {slide.EndPosition}",
                "End position must be between 1 and 8",
                startPos.Advance(content[..(slide.StartIndex + slide.SlideType!.Length)]),
                content.Length - slide.SlideType!.Length
            );
            return;
        }

        if (!IsValidSlidePath(slide.SlideType!, slide.StartPosition, slide.EndPosition.Value, slide.FlexionPoint))
        {
            var detail = GetSlidePathErrorDetail(slide.SlideType!, slide.StartPosition, slide.EndPosition.Value, slide.FlexionPoint);
            context.AddError(
                $"Invalid slide path: {slide.StartPosition}{slide.SlideType}{slide.FlexionPoint}{slide.EndPosition}",
                detail,
                startPos.Advance(content[..slide.StartIndex]),
                content.Length
            );
        }

        if (checkDuration && slide.Duration != null)
        {
            ValidateDuration(context, content, startPos, slide.Duration, slide.DurationStart, "SLIDE", allowSlideFormat: true);
        }
    }

    private static bool IsValidSlidePath(string slideType, int start, int end, int? flexionPoint)
    {
        var interval = GetPointInterval(start, end);

        return slideType switch
        {
            "-" => interval >= 2,
            "^" or "v" => interval is not (0 or 4),
            "<" or ">" => true,
            "V" => flexionPoint.HasValue &&
                   GetPointInterval(start, flexionPoint.Value) == 2 &&
                   GetPointInterval(flexionPoint.Value, end) >= 2 &&
                   start != end,
            "p" or "q" or "pp" or "qq" => true,
            "s" or "z" or "w" => interval == 4,
            _ => true
        };
    }

    private static string GetSlidePathErrorDetail(string slideType, int start, int end, int? flexionPoint)
    {
        return slideType switch
        {
            "-" => "Straight slide requires start and end positions to be at least 2 buttons apart",
            "^" or "v" => "This slide type cannot connect adjacent buttons or opposite buttons",
            "p" or "q" or "pp" or "qq" => "p/q/pp/qq slide cannot connect adjacent buttons",
            "V" => flexionPoint == null
                ? "V-shaped slide requires a flexion point, e.g., 1V35"
                : "V-shaped slide requires flexion point to be exactly 2 buttons from start, and end to be at least 2 buttons from flexion point",
            "s" or "z" or "w" => "This slide type requires start and end positions to be opposite (diagonally across)",
            _ => "Invalid slide path"
        };
    }

    private static int GetPointInterval(int a, int b)
    {
        var angleA = GetButtonAngle(a);
        var angleB = GetButtonAngle(b);
        var diff = Math.Abs(angleA - angleB);
        return Math.Min(diff / 45, 8 - diff / 45);
    }

    private static int GetButtonAngle(int button)
    {
        return button switch
        {
            8 => 0,
            1 => 45,
            2 => 90,
            3 => 135,
            4 => 180,
            5 => 225,
            6 => 270,
            7 => 315,
            _ => 0
        };
    }

    private static void CheckChartTermination(CheckerContext context, List<ChartSegment> segments)
    {
        ChartSegment? lastNonEmptySegment = null;

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var seg = segments[i];
            if (!string.IsNullOrWhiteSpace(seg.Content) && seg.Content != ",")
            {
                lastNonEmptySegment = seg;
                break;
            }
        }

        if (lastNonEmptySegment == null) return;

        if (lastNonEmptySegment.Content != "E")
        {
            context.AddWarning(
                "Chart not terminated with 'E'",
                "Simai charts should end with 'E' to mark the end of the chart",
                lastNonEmptySegment.StartPosition,
                1
            );
        }
    }

    private static int CountChar(string s, char c)
    {
        var count = 0;
        foreach (var ch in s)
        {
            if (ch == c) count++;
        }
        return count;
    }

    private static List<string> SplitByChar(string s, char c)
    {
        var result = new List<string>();
        var start = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == c)
            {
                result.Add(s[start..i]);
                start = i + 1;
            }
        }
        result.Add(s[start..]);
        return result;
    }

    private record ChartSegment(string Content, TextPosition StartPosition, int Length);

    private class NoteInfo
    {
        public int StartPosition { get; set; }
        public bool IsHold { get; set; }
        public bool IsBreak { get; set; }
        public bool IsEx { get; set; }
        public bool IsMine { get; set; }
        public bool HasStar { get; set; }
        public bool HasDoubleStar { get; set; }
        public bool NoStar { get; set; }
        public bool FadeSlide { get; set; }
        public bool NoFadeSlide { get; set; }
        public bool HasSameStartPointSlides { get; set; }
        public bool NextSlideIsSameHeadChainStart { get; set; }
        public string? Duration { get; set; }
        public int DurationStart { get; set; }
        public int DurationEnd { get; set; }
        public List<SlideInfo> Slides { get; set; } = new();
        public List<(char C, int Index)> UnknownChars { get; set; } = new();
    }

    private class SlideInfo
    {
        public string? SlideType { get; set; }
        public int StartPosition { get; set; }
        public int? EndPosition { get; set; }
        public int? FlexionPoint { get; set; }
        public string? Duration { get; set; }
        public int DurationStart { get; set; }
        public int DurationEnd { get; set; }
        public bool IsBreak { get; set; }
        public bool IsMine { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public bool IsSameHeadChainStart { get; set; }
    }

    private class CheckerContext
    {
        public string Source { get; }
        public List<SimaiDiagnostic> Diagnostics { get; } = new();
        public bool HasBpmDefinition { get; set; }

        public CheckerContext(string source)
        {
            Source = source;
        }

        public void AddError(string message, string detail, TextPosition start, int length)
        {
            Diagnostics.Add(new SimaiDiagnostic(Severity.Error, message, detail, start, length));
        }

        public void AddWarning(string message, string detail, TextPosition start, int length)
        {
            Diagnostics.Add(new SimaiDiagnostic(Severity.Warning, message, detail, start, length));
        }

        public void AddInfo(string message, string detail, TextPosition start, int length)
        {
            Diagnostics.Add(new SimaiDiagnostic(Severity.Info, message, detail, start, length));
        }
    }
}
