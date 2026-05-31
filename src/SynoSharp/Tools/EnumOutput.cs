namespace SynoSharp.Tools;

/// <summary>
/// Parses the shared shape of <c>syno*&#160;--enum</c> output, which prints a
/// <c>"N … Listed:"</c> header followed by one name per line. Header and diagnostic
/// lines contain a colon; resource names don't — so colon-bearing lines are dropped.
/// (Verified against DSM 7.1.1 for <c>synoshare</c>/<c>synouser</c>/<c>synogroup</c>.)
/// </summary>
internal static class EnumOutput
{
    public static IReadOnlyList<string> ParseNames(string standardOutput)
        => standardOutput
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.Contains(':'))
            .ToList();
}
