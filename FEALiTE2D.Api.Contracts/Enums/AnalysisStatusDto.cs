using System.Text.Json.Serialization;

namespace FEALiTE2D.Api.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnalysisStatusDto
{
    Successful,
    Failure
}
