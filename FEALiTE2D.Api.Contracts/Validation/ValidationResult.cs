using System.Collections.Generic;

namespace FEALiTE2D.Api.Contracts.Validation;

public sealed class ValidationResult
{
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;

    public void Add(string error) => Errors.Add(error);
}
