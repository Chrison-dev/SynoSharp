using System.Text.Json;
using SynoSharp;
using SynoSharp.Provisioning;
using SynoSharp.Ssh;

// synosharp — a thin CLI over the SynoSharp library.
//
// Commands:
//   discover               Web-API read → SynologySnapshot (JSON)
//   ssh-check              prove the SSH-runner transport (login + sudo + read)
//   plan  <spec.json>      diff a desired-state spec vs live → dry-run plan
//   apply <spec.json> [--confirm]   apply the plan (dry-run unless --confirm)
//
// Config (env): SYNOLOGY_BASE_URL, SYNOLOGY_USER, SYNOLOGY_PASSWORD,
//               SYNOLOGY_VERIFY_TLS (optional 'false'),
//               SYNOLOGY_SSH_HOST/PORT/KEY (optional; host falls back to BASE_URL)

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

if (command is "help" or "-h" or "--help")
{
    Console.WriteLine(
        """
        synosharp — Synology DSM client

        Usage: synosharp <command>
          discover               Dump a SynologySnapshot (DSM version, shares, users) as JSON
          ssh-check              Prove the SSH-runner: login + sudo-to-root + read-only syno* read
          plan  <spec.json>      Diff a desired-state spec against the live box (dry-run)
          apply <spec.json>      Apply the plan — dry-run unless --confirm is given
                    [--confirm]

        Config (env): SYNOLOGY_BASE_URL, SYNOLOGY_USER, SYNOLOGY_PASSWORD,
                      SYNOLOGY_VERIFY_TLS (optional 'false'),
                      SYNOLOGY_SSH_HOST/PORT/KEY (optional; host falls back to BASE_URL)
        """);
    return 0;
}

if (command is "ssh-check" or "plan" or "apply" or "exec")
{
    var sshOptions = SynologySshOptions.TryFromEnvironment();
    if (sshOptions is null)
    {
        Console.Error.WriteLine("Missing SSH config. Set SYNOLOGY_SSH_HOST (or SYNOLOGY_BASE_URL) and SYNOLOGY_USER.");
        return 2;
    }

    using var runner = new SshRunner(sshOptions);

    if (command == "ssh-check")
    {
        // 1. Transport, no root — proves SSH login works.
        var id = await runner.RunAsync(new SynologyCommand { Executable = "id", RequiresRoot = false });
        Console.WriteLine($"id            → exit {id.ExitCode}: {id.StandardOutput.Trim()}");

        // 2. sudo-to-root + a real read-only syno* read — the full risky stack, ZERO mutation.
        var shares = await runner.RunAsync(SynologyCommand.Create("synoshare", "--enum", "ALL"));
        Console.WriteLine($"synoshare ALL → exit {shares.ExitCode}");
        if (!string.IsNullOrWhiteSpace(shares.StandardOutput))
        {
            Console.WriteLine(shares.StandardOutput.TrimEnd());
        }
        return id.Success && shares.Success ? 0 : 1;
    }

    // exec — raw passthrough over the SSH-runner (sudo-to-root), for probing/diagnostics.
    if (command == "exec")
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: synosharp exec <executable> [args...]");
            return 2;
        }
        var res = await runner.RunAsync(SynologyCommand.Create(args[1], args.Skip(2).ToArray()));
        Console.WriteLine($"exit {res.ExitCode}");
        if (!string.IsNullOrWhiteSpace(res.StandardOutput)) { Console.WriteLine("--- stdout ---"); Console.WriteLine(res.StandardOutput.TrimEnd()); }
        if (!string.IsNullOrWhiteSpace(res.StandardError)) { Console.WriteLine("--- stderr ---"); Console.WriteLine(res.StandardError.TrimEnd()); }
        return res.Success ? 0 : 1;
    }

    // plan / apply
    if (args.Length < 2)
    {
        Console.Error.WriteLine($"Usage: synosharp {command} <spec.json>{(command == "apply" ? " [--confirm]" : "")}");
        return 2;
    }

    var specPath = args[1];
    if (!File.Exists(specPath))
    {
        Console.Error.WriteLine($"Spec file not found: {specPath}");
        return 2;
    }

    var desired = JsonSerializer.Deserialize<SynologyDesiredState>(
        await File.ReadAllTextAsync(specPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (desired is null)
    {
        Console.Error.WriteLine("Spec file did not parse to a desired state.");
        return 2;
    }

    var reconciler = new SynologyReconciler(runner);
    var plan = await reconciler.PlanAsync(desired);
    Console.WriteLine(plan.Render());

    if (command == "plan")
    {
        return 0;
    }

    var confirm = args.Contains("--confirm");
    if (!confirm)
    {
        Console.WriteLine("\n(dry-run — pass --confirm to apply)");
        return 0;
    }

    Console.WriteLine($"\nApplying {plan.Mutations.Count()} change(s)…");
    var result = await reconciler.ApplyAsync(plan, apply: true);
    foreach (var outcome in result.Outcomes.Where(o => o.Action.Kind != ActionKind.Skip))
    {
        Console.WriteLine($"  {outcome.Action.ResourceType} {outcome.Action.Name}: {outcome.Message}");
    }
    return result.AllSucceeded ? 0 : 1;
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
        Console.Error.WriteLine($"Unknown command '{command}'. Try: discover, ssh-check, plan, apply");
        return 1;
}
