using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FEALiTE2D.Api.Contracts;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;

namespace FEALiTE2D.Client;

/// <summary>
/// Typed HTTP client for the FEALiTE2D analysis API.
/// Designed for use through <see cref="System.Net.Http.IHttpClientFactory"/>; can also be
/// constructed manually with any pre-configured <see cref="HttpClient"/>.
/// </summary>
public sealed class FealiteApiClient
{
    private readonly HttpClient _http;

    public FealiteApiClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>
    /// Calls <c>POST /api/v1/analyze</c>. Returns the parsed
    /// <see cref="AnalysisResponse"/> for both 200 and 422 (validation/solver
    /// failures); throws <see cref="FealiteApiException"/> for other non-2xx codes
    /// or when the body cannot be parsed as a structured response.
    /// </summary>
    public async Task<AnalysisResponse> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var http = await _http.PostAsJsonAsync("api/v1/analyze", request, ApiJsonOptions.Default, cancellationToken)
                                    .ConfigureAwait(false);

        // Both 200 (Successful) and 422 (Failure) carry a structured AnalysisResponse body.
        if (http.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity)
        {
            var body = await http.Content.ReadFromJsonAsync<AnalysisResponse>(ApiJsonOptions.Default, cancellationToken)
                                          .ConfigureAwait(false);
            if (body is null)
                throw new FealiteApiException(http.StatusCode, null, "Empty response body.");
            return body;
        }

        var raw = await http.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new FealiteApiException(http.StatusCode, raw);
    }

    /// <summary>
    /// Calls <c>GET /health</c>. Returns <c>true</c> when the server responds with 2xx.
    /// </summary>
    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
