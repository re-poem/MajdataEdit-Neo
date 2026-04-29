using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
#nullable enable
namespace MajdataEdit_Neo.Extensions;

public static partial class ReadOnlySpanExtensions
{
    // ========================
    // Query
    // ========================

    public static bool Any<T>(this ReadOnlySpan<T> span)
        => !span.IsEmpty;

    public static bool Any<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
    {
        foreach (var item in span)
            if (predicate(item))
                return true;
        return false;
    }

    public static bool All<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
    {
        foreach (var item in span)
            if (!predicate(item))
                return false;
        return true;
    }

    public static int Count<T>(this ReadOnlySpan<T> span)
        => span.Length;

    public static int Count<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
    {
        int count = 0;
        foreach (var item in span)
            if (predicate(item))
                count++;
        return count;
    }

    public static bool Contains<T>(this ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>
    {
        foreach (var item in span)
            if (item.Equals(value))
                return true;
        return false;
    }

    public static T First<T>(this ReadOnlySpan<T> span)
    {
        if (span.IsEmpty)
            throw new InvalidOperationException("Sequence contains no elements.");
        return span[0];
    }

    public static T FirstOrDefault<T>(this ReadOnlySpan<T> span)
        => span.IsEmpty ? default! : span[0];

    public static T? FirstOrDefault<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
    {
        foreach (var item in span)
            if (predicate(item))
                return item;

        return default;
    }

    public static T Last<T>(this ReadOnlySpan<T> span)
    {
        if (span.IsEmpty)
            throw new InvalidOperationException("Sequence contains no elements.");
        return span[^1];
    }

    public static T LastOrDefault<T>(this ReadOnlySpan<T> span)
        => span.IsEmpty ? default! : span[^1];

    // ========================
    // Projection
    // ========================

    public static TResult[] Select<T, TResult>(
        this ReadOnlySpan<T> span,
        Func<T, TResult> selector)
    {
        var result = new TResult[span.Length];

        for (int i = 0; i < span.Length; i++)
            result[i] = selector(span[i]);

        return result;
    }

    public static T[] Where<T>(
        this ReadOnlySpan<T> span,
        Func<T, bool> predicate)
    {
        var list = new List<T>();

        foreach (var item in span)
            if (predicate(item))
                list.Add(item);

        return list.ToArray();
    }

    // ========================
    // Aggregate
    // ========================

    public static T Sum<T>(this ReadOnlySpan<T> span)
        where T : struct
    {
        dynamic sum = default(T)!;

        foreach (var item in span)
            sum += (dynamic)item;

        return sum;
    }

    public static TResult Aggregate<T, TResult>(
        this ReadOnlySpan<T> span,
        TResult seed,
        Func<TResult, T, TResult> func)
    {
        TResult acc = seed;

        foreach (var item in span)
            acc = func(acc, item);

        return acc;
    }

    public static T? Min<T>(this ReadOnlySpan<T> span)
        where T : IComparable<T>
    {
        if (span.IsEmpty) return default;

        var min = span[0];

        for (int i = 1; i < span.Length; i++)
            if (span[i].CompareTo(min) < 0)
                min = span[i];

        return min;
    }

    public static T? Max<T>(this ReadOnlySpan<T> span)
        where T : IComparable<T>
    {
        if (span.IsEmpty) return default;

        var max = span[0];

        for (int i = 1; i < span.Length; i++)
            if (span[i].CompareTo(max) > 0)
                max = span[i];

        return max;
    }

    // ========================
    // Conversion
    // ========================

    public static T[] ToArray<T>(this ReadOnlySpan<T> span)
    {
        var arr = new T[span.Length];
        span.CopyTo(arr);
        return arr;
    }

    public static List<T> ToList<T>(this ReadOnlySpan<T> span)
    {
        var list = new List<T>(span.Length);

        foreach (var item in span)
            list.Add(item);

        return list;
    }

    // ========================
    // Slice Helpers
    // ========================

    public static ReadOnlySpan<T> Skip<T>(this ReadOnlySpan<T> span, int count)
        => span[count..];

    public static ReadOnlySpan<T> Take<T>(this ReadOnlySpan<T> span, int count)
        => span[..Math.Min(count, span.Length)];

    public static ReadOnlySpan<T> SkipLast<T>(this ReadOnlySpan<T> span, int count)
        => span[..Math.Max(0, span.Length - count)];

    public static ReadOnlySpan<T> TakeLast<T>(this ReadOnlySpan<T> span, int count)
        => span[Math.Max(0, span.Length - count)..];
    
    public static TSource? MinBy<TSource, TKey>(
        this ReadOnlySpan<TSource> span,
        Func<TSource, TKey> selector)
        where TKey : IComparable<TKey>
    {
        if (span.IsEmpty) return default;

        var bestItem = span[0];
        var bestKey = selector(bestItem);

        for (int i = 1; i < span.Length; i++)
        {
            var item = span[i];
            var key = selector(item);

            if (key.CompareTo(bestKey) < 0)
            {
                bestKey = key;
                bestItem = item;
            }
        }

        return bestItem;
    }

    public static TSource? MaxBy<TSource, TKey>(
        this ReadOnlySpan<TSource> span,
        Func<TSource, TKey> selector)
        where TKey : IComparable<TKey>
    {
        if (span.IsEmpty) return default;

        var bestItem = span[0];
        var bestKey = selector(bestItem);

        for (int i = 1; i < span.Length; i++)
        {
            var item = span[i];
            var key = selector(item);

            if (key.CompareTo(bestKey) > 0)
            {
                bestKey = key;
                bestItem = item;
            }
        }

        return bestItem;
    }
}
