namespace Granit.Tax;

/// <summary>
/// Validates tax identification numbers online (VIES, HMRC, Stripe Tax IDs).
/// </summary>
/// <remarks>
/// For offline format validation, use <c>Granit.Validation.Europe</c> FluentValidation
/// extensions instead. This interface performs online verification against tax authorities.
/// </remarks>
public interface ITaxIdValidator
{
    /// <summary>Provider name (e.g., "vies", "stripe-tax", "hmrc").</summary>
    string Name { get; }

    /// <summary>Validates a tax ID online.</summary>
    Task<TaxIdValidationResult> ValidateAsync(
        string taxId, string countryCode,
        CancellationToken cancellationToken = default);
}
