using System;

namespace MajdataEdit_Neo.Models.SimaiChecker;

public readonly struct TextPosition(int absolute, int line, int column) : IComparable<TextPosition>, IEquatable<TextPosition>
{
    public int Absolute { get; } = absolute;
    public int Line { get; } = line;
    public int Column { get; } = column;

    public static TextPosition Start => new(0, 1, 1);

    public TextPosition Advance(char c)
    {
        if (c == '\n')
            return new TextPosition(Absolute + 1, Line + 1, 1);
        return new TextPosition(Absolute + 1, Line, Column + 1);
    }

    public TextPosition Advance(string s)
    {
        var pos = this;
        foreach (var c in s)
            pos = pos.Advance(c);
        return pos;
    }

    public int CompareTo(TextPosition other) => Absolute.CompareTo(other.Absolute);
    public bool Equals(TextPosition other) => Absolute == other.Absolute;
    public override bool Equals(object? obj) => obj is TextPosition pos && Equals(pos);
    public override int GetHashCode() => Absolute;
    public override string ToString() => $"Line {Line}, Column {Column}";

    public static bool operator <(TextPosition left, TextPosition right) => left.Absolute < right.Absolute;
    public static bool operator >(TextPosition left, TextPosition right) => left.Absolute > right.Absolute;
    public static bool operator <=(TextPosition left, TextPosition right) => left.Absolute <= right.Absolute;
    public static bool operator >=(TextPosition left, TextPosition right) => left.Absolute >= right.Absolute;
}
