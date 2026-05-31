using System.Net.Http.Json;
using System.Text.Json;

namespace SynoSharp;

/// <summary>
/// Thin Synology DSM Web API client for the read/discover path: logs in via
/// <c>SYNO.API.Auth</c> and issues <c>entry.cgi</c> calls, unwrapping the
/// <c>{ "success": …, "data": … }</c> envelope.
/// <para>
/// The system-admin endpoints (<c>SYNO.Core.*</c>) are undocumented and
/// version-fragile (ADR-0002) — this targets DSM 7.1. Mutations go through an
/// SSH-runner (separate, later); this client is read-only.
/// </para>
/// </summary>
public sealed class SynologyApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly SynologyOptions _options;
    private readonly bool _ownsHttp;
    private string? _sid;

    public SynologyApiClient(SynologyOptions options, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        if (httpClient is null)
        {
            var handler = new HttpClientHandler();
            if (!options.VerifyTls)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            _http = new HttpClient(handler);
            _ownsHttp = true;
        }
        else
        {
            _http = httpClient;
            _ownsHttp = false;
        }

        _http.BaseAddress = options.BaseUrl.AbsoluteUri.EndsWith('/')
            ? options.BaseUrl
            : new Uri(options.BaseUrl.AbsoluteUri + "/");
    }

    /// <summary>Authenticate (<c>SYNO.API.Auth</c>) and cache the session id.</summary>
    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        var query = $"webapi/auth.cgi?api=SYNO.API.Auth&version=3&method=login" +
            $"&account={Uri.EscapeDataString(_options.Username)}" +
            $"&passwd={Uri.EscapeDataString(_options.Password)}&session=DSM&format=sid";

        var envelope = await _http.GetFromJsonAsync<JsonElement>(query, cancellationToken).ConfigureAwait(false);
        if (!envelope.TryGetProperty("success", out var ok) || !ok.GetBoolean())
        {
            throw new InvalidOperationException("Synology login failed (SYNO.API.Auth).");
        }
        _sid = envelope.GetProperty("data").GetProperty("sid").GetString();
    }

    /// <summary>
    /// Call an <c>entry.cgi</c> API method and return its <c>data</c> element
    /// (logs in first if needed). <paramref name="extraQuery"/> is appended raw.
    /// </summary>
    public async Task<JsonElement> GetAsync(
        string api, int version, string method, string? extraQuery = null, CancellationToken cancellationToken = default)
    {
        if (_sid is null)
        {
            await LoginAsync(cancellationToken).ConfigureAwait(false);
        }

        var query = $"webapi/entry.cgi?api={api}&version={version}&method={method}&_sid={_sid}";
        if (!string.IsNullOrEmpty(extraQuery))
        {
            query += "&" + extraQuery;
        }

        var envelope = await _http.GetFromJsonAsync<JsonElement>(query, cancellationToken).ConfigureAwait(false);
        if (!envelope.TryGetProperty("success", out var ok) || !ok.GetBoolean())
        {
            throw new InvalidOperationException($"Synology API {api}.{method} failed.");
        }
        return envelope.TryGetProperty("data", out var data) ? data : default;
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
