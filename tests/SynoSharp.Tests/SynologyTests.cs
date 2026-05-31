using Xunit;

namespace SynoSharp.Tests;

public class SynologyOptionsTests
{
    [Fact]
    public void VerifyTls_defaults_to_true()
    {
        var options = new SynologyOptions
        {
            BaseUrl = new Uri("https://nas:5001"),
            Username = "u",
            Password = "p",
        };

        Assert.True(options.VerifyTls);
    }
}

/// <summary>
/// Read-only integration test against a DSM target — a Virtual DSM on a
/// KVM-capable host or the live NAS. Runs only when SYNOLOGY_* env vars are set;
/// otherwise skips. (The Virtual DSM container can't run on Apple Silicon.)
/// </summary>
public class SynologyLiveTests
{
    [SkippableFact]
    public async Task Discover_returns_a_snapshot()
    {
        var options = SynologyOptions.TryFromEnvironment();
        Skip.If(options is null, "No SYNOLOGY_* env — skipping live DSM discovery.");

        using var client = new SynologyApiClient(options!);
        var snapshot = await new SynologyDiscovery(client).DiscoverAsync();

        Assert.NotNull(snapshot);
    }
}
