namespace Granit.Validation.ServerValidation;

/// <summary>
/// Wraps a <see cref="Func{T, TResult}"/> delegate as an <see cref="IServerValidator"/>.
/// </summary>
/// <remarks>
/// Use this class in <see cref="IServerValidatorContributor"/> implementations to avoid
/// creating a dedicated class for each validator:
/// <code>
/// yield return new DelegatingServerValidator(
///     "Granit:Validation:InvalidIban", IbanAlgorithm.IsValid);
/// </code>
/// </remarks>
public sealed class DelegatingServerValidator(string errorCode, Func<string?, bool> validateFunc, bool isSensitive = false)
    : IServerValidator
{
    private readonly Func<string?, bool> _validateFunc = validateFunc
        ?? throw new ArgumentNullException(nameof(validateFunc));

    /// <inheritdoc />
    public string ErrorCode { get; } = errorCode
        ?? throw new ArgumentNullException(nameof(errorCode));

    /// <inheritdoc />
    public bool IsSensitive { get; } = isSensitive;

    /// <inheritdoc />
    public bool Validate(string? value) => _validateFunc(value);
}
