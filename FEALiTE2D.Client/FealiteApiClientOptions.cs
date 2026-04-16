using System;

namespace FEALiTE2D.Client;

public sealed class FealiteApiClientOptions
{
    /// <summary>Logical configuration section name (<c>FealiteApi</c>).</summary>
    public const string SectionName = "FealiteApi";

    /// <summary>Base URL of the FEALiTE2D HTTP API (e.g. <c>http://localhost:5180/</c>).</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Per-request timeout. Default 60 s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}
