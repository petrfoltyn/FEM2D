using System;
using System.Net;

namespace FEALiTE2D.Client;

/// <summary>
/// Thrown when the FEALiTE2D API returns an unexpected HTTP status code that
/// the client cannot map onto a structured <see cref="Contracts.Responses.AnalysisResponse"/>.
/// </summary>
public sealed class FealiteApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public FealiteApiException(HttpStatusCode statusCode, string? body, string? message = null)
        : base(message ?? $"FEALiTE2D API returned {(int)statusCode} {statusCode}.")
    {
        StatusCode = statusCode;
        ResponseBody = body;
    }
}
