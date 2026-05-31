namespace SynoSharp.Ssh;

/// <summary>Result of running a <see cref="SynologyCommand"/> over SSH.</summary>
public sealed record SshCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}
