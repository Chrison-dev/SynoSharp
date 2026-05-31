using SynoSharp.Ssh;

namespace SynoSharp.Tools;

/// <summary>Thrown when an on-box <c>syno*</c> tool returns a non-zero exit code.</summary>
public sealed class SynologyToolException : Exception
{
    public SshCommandResult Result { get; }

    public SynologyToolException(string what, SshCommandResult result)
        : base($"{what} failed (exit {result.ExitCode}): {result.StandardError.Trim()}")
    {
        Result = result;
    }
}
