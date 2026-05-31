using SynoSharp.Ssh;
using Xunit;

namespace SynoSharp.Tests;

public class SynologyCommandTests
{
    [Fact]
    public void Render_leaves_safe_word_tokens_bare()
    {
        var cmd = SynologyCommand.Create("synoshare", "--enum", "ALL");
        Assert.Equal("synoshare --enum ALL", cmd.Render());
    }

    [Fact]
    public void Render_single_quotes_tokens_with_spaces()
    {
        var cmd = SynologyCommand.Create("synoshare", "--add", "My Share");
        Assert.Equal("synoshare --add 'My Share'", cmd.Render());
    }

    [Fact]
    public void Render_escapes_embedded_single_quotes()
    {
        // A share name with an apostrophe must not break out of its quoting.
        var cmd = SynologyCommand.Create("synoshare", "--add", "Chris's Files");
        Assert.Equal("synoshare --add 'Chris'\\''s Files'", cmd.Render());
    }

    [Fact]
    public void RequiresRoot_defaults_to_true()
    {
        Assert.True(SynologyCommand.Create("synouser").RequiresRoot);
    }
}

/// <summary>
/// Read-only SSH-runner integration test against the live NAS. Runs only when the
/// SSH env is set; otherwise skips. Asserts the non-root transport (<c>id</c>) —
/// proving SSH login works without depending on sudo being configured.
/// </summary>
public class SshRunnerLiveTests
{
    [SkippableFact]
    public async Task Id_returns_a_uid_over_ssh()
    {
        var options = SynologySshOptions.TryFromEnvironment();
        Skip.If(options is null || string.IsNullOrEmpty(options.Password),
            "No SYNOLOGY_SSH/USER/PASSWORD env — skipping live SSH-runner test.");

        using var runner = new SshRunner(options!);
        var result = await runner.RunAsync(new SynologyCommand { Executable = "id", RequiresRoot = false });

        Assert.True(result.Success, $"`id` failed (exit {result.ExitCode}): {result.StandardError}");
        Assert.Contains("uid=", result.StandardOutput);
    }
}
