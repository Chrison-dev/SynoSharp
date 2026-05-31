using System.Text.Json;
using SynoSharp;

// synosharp — a thin read-only CLI over the SynoSharp library.
//
// Commands: discover
// Config (env): SYNOLOGY_BASE_URL (e.g. https://nas:5001), SYNOLOGY_USER,
//               SYNOLOGY_PASSWORD, SYNOLOGY_VERIFY_TLS (optional, 'false')

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

if (command is "help" or "-h" or "--help")
{
    Console.WriteLine(
        """
        synosharp — read-only Synology DSM client

        Usage: synosharp <command>
          discover   Dump a SynologySnapshot (DSM version, shares, users) as JSON

        Config (env): SYNOLOGY_BASE_URL (e.g. https://nas:5001), SYNOLOGY_USER,
                      SYNOLOGY_PASSWORD, SYNOLOGY_VERIFY_TLS (optional, 'false')
        """);
    return 0;
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
        Console.Error.WriteLine($"Unknown command '{command}'. Try: discover");
        return 1;
}
