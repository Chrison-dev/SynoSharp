namespace SynoSharp.Tools;

/// <summary>
/// Parses the <c>label … [value]</c> shape of <c>syno*&#160;--get</c> / <c>--descget</c>
/// output into a label→value map. Works for both <c>synoshare --get</c> (dotted:
/// <c>Comment ....[x]</c>) and <c>synouser --get</c> (colon: <c>User Mail : [x]</c>):
/// the value is the text inside the line's first <c>[...]</c>, and the label is what
/// precedes it (dots/colons/whitespace trimmed). Verified against DSM 7.1.1.
/// </summary>
internal static class GetFields
{
    public static IReadOnlyDictionary<string, string> Parse(string standardOutput)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in standardOutput.Split('\n'))
        {
            var line = raw.Trim();
            var open = line.IndexOf('[');
            var close = line.LastIndexOf(']');
            if (open < 0 || close <= open)
            {
                continue;
            }
            var label = line[..open].Trim(' ', '.', ':', '\t');
            if (label.Length > 0 && !map.ContainsKey(label))
            {
                map[label] = line[(open + 1)..close];
            }
        }
        return map;
    }

    /// <summary>The text inside the first <c>[...]</c> anywhere in the output (e.g. <c>--descget</c>).</summary>
    public static string? FirstBracket(string standardOutput)
    {
        var open = standardOutput.IndexOf('[');
        if (open < 0)
        {
            return null;
        }
        var close = standardOutput.IndexOf(']', open + 1);
        return close > open ? standardOutput[(open + 1)..close] : null;
    }
}
