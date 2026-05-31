using System.Text.Json;
using SynoSharp;
using SynoSharp.Ssh;

// synosharp — a thin CLI over the SynoSharp library.
//
// Commands: discover (Web-API read), ssh-check (prove the SSH-runner transport)
// Config (env): SYNOLOGY_BASE_URL (e.g. https://nas:5001), SYNOLOGY_USER,
//               SYNOLOGY_PASSWORD, SYNOLOGY_VERIFY_TLS (optional, 'false'),
//               SYNOLOGY_SSH_HOST/PORT/KEY (optional; host falls back to BASE_URL)

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

if (command is "help" or "-h" or "--help")
{
    Console.WriteLine(
        """
        synosharp — Synology DSM client

        Usage: synosharp <command>
          discover    Dump a SynologySnapshot (DSM version, shares, users) as JSON
          ssh-check   Prove the SSH-runner: SSH login + sudo-to-root + a read-only syno* read

        Config (env): SYNOLOGY_BASE_URL (e.g. https://nas:5001), SYNOLOGY_USER,
                      SYNOLOGY_PASSWORD, SYNOLOGY_VERIFY_TLS (optional, 'false'),
                      SYNOLOGY_SSH_HOST/PORT/KEY (optional; host falls back to BASE_URL)
        """);
    return 0;
}

if (command == "ssh-check")
{
    var sshOptions = SynologySshOptions.TryFromEnvironment();
    if (sshOptions is null)
    {
        Console.Error.WriteLine("Missing SSH config. Set SYNOLOGY_SSH_HOST (or SYNOLOGY_BASE_URL) and SYNOLOGY_USER.");
        return 2;
    }

    using var runner = new SshRunner(sshOptions);

    // 1. Transport, no root — proves SSH login works.
    var id = await runner.RunAsync(new SynologyCommand { Executable = "id", RequiresRoot = false });
    Console.WriteLine($"id            → exit {id.ExitCode}: {id.StandardOutput.Trim()}");

    // 2. sudo-to-root + a real read-only syno* read — proves the full risky stack
    //    (sudo via stdin → root → on-box CLI) with ZERO mutation.
    var shares = await runner.RunAsync(SynologyCommand.Create("synoshare", "--enum", "ALL"));
    Console.WriteLine($"synoshare ALL → exit {shares.ExitCode}");
    if (!string.IsNullOrWhiteSpace(shares.StandardOutput))
    {
        Console.WriteLine(shares.StandardOutput.TrimEnd());
    }
    if (!string.IsNullOrWhiteSpace(shares.StandardError))
    {
        Console.Error.WriteLine(shares.StandardError.TrimEnd());
    }

    return id.Success && shares.Success ? 0 : 1;
}

var options = SynologyOptions.TryFromEnvironment();
if (options is null)
{
    Console.Error.WriteLine("Missing config. Set SYNOLOGY_BASE_URL, SYNOLOGY_USER, SYNOLOGY_PASSWORD.");
    return 2;
}

using var client = new SynologyApiClient(options);

switch (command)
{
    case "discover":
        var snapshot = await new SynologyDiscovery(client).DiscoverAsync();
        Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
        return 0;

    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Try: discover, ssh-check");
        return 1;
}
