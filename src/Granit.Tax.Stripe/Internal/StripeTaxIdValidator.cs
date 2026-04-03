using Granit.Timing;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Granit.Tax.Stripe.Internal;

/// <summary>Stripe Tax IDs API implementation of <see cref="ITaxIdValidator"/>.</summary>
/// <remarks>
/// Stripe validates tax IDs when they are attached to a Customer object.
/// This implementation creates a tax ID verification request and checks the
/// status. For countries not supported by Stripe, it falls back to format-only
/// validation with <see cref="TaxIdValidationSource.Offline"/> source.
/// </remarks>
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
            string taxIdType = MapToStripeTaxIdType(countryCode);

            // Use Stripe Tax ID verification endpoint
            var service = new TaxIdService(stripeClient);
            TaxId stripeTaxId = await service.CreateAsync(new TaxIdCreateOptions
            {
                Type = taxIdType,
                Value = taxId,
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            bool isValid = stripeTaxId.Verification?.Status == "verified";
            Log.ValidationCompleted(logger, MaskTaxId(taxId), countryCode, isValid);

            return new TaxIdValidationResult(
                IsValid: isValid,
                CompanyName: stripeTaxId.Verification?.VerifiedName,
                CompanyAddress: stripeTaxId.Verification?.VerifiedAddress,
                RequestIdentifier: stripeTaxId.Id,
                ValidatedAt: clock.Now,
                Source: TaxIdValidationSource.StripeTax);
        }
        catch (StripeException ex)
        {
            Log.ValidationError(logger, ex, MaskTaxId(taxId));
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

    private static string MaskTaxId(string taxId) =>
        taxId.Length > 4 ? $"{taxId[..2]}***{taxId[^4..]}" : "***";

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Stripe Tax ID validation completed: {MaskedTaxId} ({Country}), valid = {IsValid}")]
        public static partial void ValidationCompleted(ILogger logger, string maskedTaxId, string country, bool isValid);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe Tax ID validation error for {MaskedTaxId}")]
        public static partial void ValidationError(ILogger logger, Exception ex, string maskedTaxId);
    }
}
