using System.Text.Json;
using System.Text.Json.Serialization;

namespace FEALiTE2D.Api.Contracts;

/// <summary>
/// Default <see cref="JsonSerializerOptions"/> for FEALiTE2D HTTP API contracts.
/// Uses camelCase property naming (uppercase physical-quantity names like
/// <c>E</c>, <c>A</c>, <c>Iz</c> are preserved via per-property
/// <c>[JsonPropertyName]</c> attributes on the DTOs).
/// </summary>
public static class ApiJsonOptions
{
    public static JsonSerializerOptions Default { get; } = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Dictionary keys are user-supplied labels (load-case names, node labels,
            // load-combination factors) — they must be passed through unchanged.
            DictionaryKeyPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // .NET 9+: polymorphic "type" discriminator may appear anywhere in the
            // JSON object instead of being forced to first position.
            AllowOutOfOrderMetadataProperties = true
        };
        return options;
    }
}
