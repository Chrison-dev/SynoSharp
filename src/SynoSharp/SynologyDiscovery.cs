using System.Text.Json;

namespace SynoSharp;

/// <summary>
/// Read-only discovery over the DSM Web API → a structured <see cref="SynologySnapshot"/>.
/// <para>
/// The <c>SYNO.Core.*</c> endpoints are undocumented/version-fragile (ADR-0002),
/// so each read is defensive — a differing shape on a given DSM build degrades to
/// an empty result rather than throwing. <b>Wired against DSM 7.1 but UNVERIFIED</b>
/// until a DSM target is available (the Virtual DSM container needs KVM/x86, so it
/// can't run on Apple Silicon — verify on a Linux host or the live NAS read-only).
/// </para>
/// </summary>
public sealed class SynologyDiscovery
{
    private readonly SynologyApiClient _client;

    public SynologyDiscovery(SynologyApiClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<SynologySnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var shares = await SafeListNamesAsync("SYNO.Core.Share", 1, "list", "shares", cancellationToken).ConfigureAwait(false);
        var users = await SafeListNamesAsync("SYNO.Core.User", 1, "list", "users", cancellationToken).ConfigureAwait(false);

        string? version = null;
        string? hostname = null;
        try
        {
            var info = await _client.GetAsync("SYNO.Core.System", 1, "info", cancellationToken: cancellationToken).ConfigureAwait(false);
            if (info.ValueKind == JsonValueKind.Object)
            {
                if (info.TryGetProperty("firmware_ver", out var fv))
                {
                    version = fv.GetString();
                }
                if (info.TryGetProperty("hostname", out var hn))
                {
                    hostname = hn.GetString();
                }
            }
        }
        catch
        {
            // SYNO.Core.System shape varies by DSM build; tolerate.
        }

        return new SynologySnapshot
        {
            DsmVersion = version,
            Hostname = hostname,
            Shares = shares,
            Users = users,
        };
    }

    private async Task<IReadOnlyList<string>> SafeListNamesAsync(
        string api, int version, string method, string arrayProperty, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _client.GetAsync(api, version, method, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty(arrayProperty, out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                return arr.EnumerateArray()
                    .Select(e => e.TryGetProperty("name", out var n) ? n.GetString() : null)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s!)
                    .ToList();
            }
        }
        catch
        {
            // Undocumented endpoint may be absent/renamed on this DSM build; tolerate.
        }
        return [];
    }
}
