namespace SynoSharp.Ssh;

/// <summary>
/// Executes structured on-box commands over SSH — the mutation transport for
/// SynoSharp (ADR-0002: <c>syno*</c> CLI + <c>synowebapi --exec</c>, sudo-to-root).
/// Typed <c>Ensure*</c> operations (later) build on this; this interface is the
/// raw transport so it can be faked in tests.
/// </summary>
public interface ISshRunner
{
    Task<SshCommandResult> RunAsync(SynologyCommand command, CancellationToken cancellationToken = default);
}
