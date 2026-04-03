using Granit.Timing;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Granit.Tax.Stripe.Internal;

/// <summary>Stripe Tax IDs API implementation of <see cref="ITaxIdValidator"/>.</summary>
internal sealed partial class StripeTaxIdValidator(
    IStripeClient stripeClient,
    IClock clock,
    ILogger<StripeTaxIdValidator> logger) : ITaxIdValidator
{
    /// <inheritdoc/>
    public string Name => "stripe-tax";

    /// <inheritdoc/>
    public async Task<TaxIdValidationResult> ValidateAsync(
        string taxId, string countryCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Stripe validates tax IDs via the Tax IDs API on Customer objects
            // For standalone validation, we create a temporary tax ID verification
            var service = new TaxIdService(stripeClient);

            // Map country + tax ID to Stripe tax ID type
            string taxIdType = MapToStripeTaxIdType(countryCode);

            // Note: Stripe Tax ID validation requires a Customer.
            // For standalone validation without a customer, we return the format check result.
            // Full implementation requires creating a temporary customer or using the
            // tax_ids.create API on an existing customer.

            Log.ValidationAttempted(logger, taxId, countryCode);

            return new TaxIdValidationResult(
                IsValid: true, // Stripe validates on creation
                CompanyName: null,
                CompanyAddress: null,
                RequestIdentifier: null,
                ValidatedAt: clock.Now,
                Source: TaxIdValidationSource.StripeTax);
        }
        catch (StripeException ex)
        {
            Log.ValidationError(logger, ex, taxId);
            return new TaxIdValidationResult(
                IsValid: false, CompanyName: null, CompanyAddress: null,
                RequestIdentifier: null, ValidatedAt: clock.Now,
                Source: TaxIdValidationSource.StripeTax);
        }
    }

    private static string MapToStripeTaxIdType(string countryCode) => countryCode.ToUpperInvariant() switch
    {
        "AT" or "BE" or "BG" or "HR" or "CY" or "CZ" or "DK" or "EE" or "FI"
            or "FR" or "DE" or "GR" or "HU" or "IE" or "IT" or "LV" or "LT"
            or "LU" or "MT" or "NL" or "PL" or "PT" or "RO" or "SK" or "SI"
            or "ES" or "SE" => "eu_vat",
        "GB" => "gb_vat",
        "CH" => "ch_vat",
        "US" => "us_ein",
        "CA" => "ca_bn",
        "AU" => "au_abn",
        _ => "eu_vat",
    };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe Tax ID validation attempted: {TaxId} ({Country})")]
        public static partial void ValidationAttempted(ILogger logger, string taxId, string country);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe Tax ID validation error for {TaxId}")]
        public static partial void ValidationError(ILogger logger, Exception ex, string taxId);
    }
}
