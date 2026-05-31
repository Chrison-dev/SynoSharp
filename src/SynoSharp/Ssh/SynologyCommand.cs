namespace SynoSharp.Ssh;

/// <summary>
/// A structured on-box command (executable + argv) for the SSH-runner. Inputs go
/// in as discrete argv elements and are shell-quoted only at render time, so
/// quoting/injection on the root shell isn't a footgun (ADR-0002). Most <c>syno*</c>
/// CLIs require root; DSM 7 has no direct root SSH, so the runner sudo's by default.
/// </summary>
public sealed record SynologyCommand
{
    public required string Executable { get; init; }

    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>
    /// The <c>syno*</c> CLIs generally require root. DSM 7 disables direct root
    /// SSH, so the runner runs this under <c>sudo</c> when true (the default).
    /// </summary>
    public bool RequiresRoot { get; init; } = true;

    public static SynologyCommand Create(string executable, params string[] args)
        => new() { Executable = executable, Args = args };

    /// <summary>Single-line, shell-quoted rendering — for dry-run display and execution.</summary>
    public string Render() => string.Join(' ', new[] { Executable }.Concat(Args).Select(Quote));

    /// <summary>
    /// Shell-quote a token: bare if it's a safe "word" char set, else single-quoted
    /// with embedded single-quotes escaped (<c>'</c> → <c>'\''</c>).
    /// </summary>
    private static string Quote(string s)
    {
        if (s.Length > 0 && s.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/' or '.' or '=' or ':' or ','))
        {
            return s;
        }
        return "'" + s.Replace("'", "'\\''") + "'";
    }
}
