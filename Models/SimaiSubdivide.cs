using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MajdataEdit_Neo.Models;

public class SimaiSubdivide
{
    public static string Subdivide(string phrase, float multiplier)
    {
        var strs = phrase.Split('{', StringSplitOptions.RemoveEmptyEntries);
        string result = "";

        foreach (var str in strs)
        {
            result += SubdivideInternal("{" + str, multiplier);
        }

        return result;
    }

    private static string SubdivideInternal(string phrase, float multiplier)
    {
        if (multiplier <= 1)
        {
            return phrase; // Invalid multiplier, return original phrase
        }

        int startIndex = phrase.IndexOf('{') + 1;
        int endIndex = phrase.IndexOf('}');

        if (startIndex == 0 || endIndex == -1 || endIndex <= startIndex)
        {
            return phrase; // Invalid format, return original phrase
        }

        // Extract the subdivision number and content inside the brackets
        int originalSubdivision = int.Parse(phrase[startIndex..endIndex]);
        string content = phrase[(endIndex + 1)..];

        // Calculate the new subdivision (round to nearest integer)
        double newSubdivision = originalSubdivision * multiplier;
        double diff = Math.Abs(Math.Truncate(newSubdivision) - newSubdivision);
        if (!(diff < 0.0000001 || diff > 0.9999999))
        {
            return phrase; // Subdivision cannot be a non-integer, return original phrase
        }

        var newCommaCount = content.Count(c => c == ',') * multiplier;
        double diff1 = Math.Abs(Math.Truncate(newCommaCount) - newCommaCount);
        if (!(diff1 < 0.0000001 || diff1 > 0.9999999))
        {
            return phrase; // comma count cannot be a non-integer, return original phrase
        }

        // Process only the commas in the content
        var expandedContent = new StringBuilder();
        double fractionalComma = 0.0;

        foreach (char c in content)
        {
            if (c == ',')
            {
                fractionalComma += multiplier;
                int repeatCount = (int)fractionalComma;
                fractionalComma -= repeatCount;
                expandedContent.Append(new string(',', repeatCount));
            }
            else
            {
                expandedContent.Append(c);
            }
        }

        return $"{{{newSubdivision}}}{expandedContent}";
    }
}
