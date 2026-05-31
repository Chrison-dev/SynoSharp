using System.Text;
using Renci.SshNet;

namespace SynoSharp.Ssh;

/// <summary>
/// SSH.NET-backed <see cref="ISshRunner"/>. Connects lazily and, for commands that
/// <see cref="SynologyCommand.RequiresRoot"/>, runs them under <c>sudo -S</c>,
/// feeding the password over <b>stdin</b> — so the secret never appears in the
/// command string, the process list, or a dry-run render.
/// </summary>
public sealed class SshRunner : ISshRunner, IDisposable
{
    // DSM keeps the syno* CLIs under /usr/syno/{sbin,bin}, which sudo's secure_path
    // excludes — so root commands are run through `env` with this PATH prepended.
    private const string DsmToolPath = "/usr/syno/sbin:/usr/syno/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin";

    private readonly SynologySshOptions _options;
    private readonly SshClient _client;

    public SshRunner(SynologySshOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _client = BuildClient(options);

        if (options.AcceptAnyHostKey)
        {
            _client.HostKeyReceived += (_, e) => e.CanTrust = true;
        }
    }

    private static SshClient BuildClient(SynologySshOptions o)
    {
        if (!string.IsNullOrEmpty(o.PrivateKeyPath))
        {
            var keyFile = new PrivateKeyFile(o.PrivateKeyPath);
            var auth = new PrivateKeyAuthenticationMethod(o.Username, keyFile);
            return new SshClient(new ConnectionInfo(o.Host, o.Port, o.Username, auth));
        }

        if (string.IsNullOrEmpty(o.Password))
        {
            throw new InvalidOperationException(
                "SynologySshOptions needs a Password or PrivateKeyPath to connect.");
        }

        var pwAuth = new PasswordAuthenticationMethod(o.Username, o.Password);
        return new SshClient(new ConnectionInfo(o.Host, o.Port, o.Username, pwAuth));
    }

    public async Task<SshCommandResult> RunAsync(SynologyCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_client.IsConnected)
        {
            await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        var sudo = command.RequiresRoot;
        // -S: read the password from stdin; -p '': suppress the prompt so it never
        // pollutes stdout. The password is written to stdin below, not interpolated.
        // `/usr/bin/env PATH=…` makes the syno* tools resolvable under sudo's secure_path.
        var text = sudo
            ? $"sudo -S -p '' /usr/bin/env PATH={DsmToolPath} {command.Render()}"
            : command.Render();

        using var cmd = _client.CreateCommand(text);

        if (sudo)
        {
            if (string.IsNullOrEmpty(_options.Password))
            {
                throw new InvalidOperationException(
                    "A root command requires a Password for sudo, but none was provided.");
            }

            // The input stream is only valid once the channel is open, i.e. after
            // execution has started — so kick off ExecuteAsync first, then write.
            var exec = cmd.ExecuteAsync(cancellationToken);
            using (var input = cmd.CreateInputStream())
            {
                await input.WriteAsync(Encoding.UTF8.GetBytes(_options.Password + "\n"), cancellationToken).ConfigureAwait(false);
            }
            await exec.ConfigureAwait(false);
        }
        else
        {
            await cmd.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }

        return new SshCommandResult(cmd.ExitStatus ?? -1, cmd.Result ?? string.Empty, cmd.Error ?? string.Empty);
    }

    public void Dispose() => _client.Dispose();
}
