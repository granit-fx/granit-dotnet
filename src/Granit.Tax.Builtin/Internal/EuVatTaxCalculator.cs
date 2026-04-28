using Granit.Invoicing;
using Granit.Invoicing.Dtos;
using Granit.MultiTenancy;
using Granit.Tax.Diagnostics;
using Granit.Tax.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.Tax.Builtin.Internal;

/// <summary>
/// Self-hosted EU VAT implementation of <see cref="ITaxCalculator"/>.
/// </summary>
/// <remarks>
/// Orchestrates the EU VAT rule engine, rate provider, and VIES validation
/// to compute per-line tax amounts for invoices.
/// </remarks>
internal sealed class EuVatTaxCalculator(
    ITaxRateProvider rateProvider,
    ITaxIdValidator taxIdValidator,
    IOptions<TaxOptions> taxOptions,
    IClock clock,
    TaxMetrics metrics,
    ICurrentTenant currentTenant) : ITaxCalculator
{
    /// <inheritdoc/>
    public string Name => "eu-vat";

    /// <inheritdoc/>
    public async Task<TaxResult> CalculateAsync(
        TaxRequest request, CancellationToken cancellationToken = default)
    {
        TaxOptions options = taxOptions.Value;

        // Determine if buyer has a valid VAT number
        bool buyerHasValidVat = false;
        if (!string.IsNullOrWhiteSpace(request.BuyerAddress.VatNumber))
        {
            TaxIdValidationResult validation = await taxIdValidator
                .ValidateAsync(request.BuyerAddress.VatNumber, request.BuyerAddress.Country, cancellationToken)
                .ConfigureAwait(false);

            buyerHasValidVat = validation.IsValid;
        }

        // Classify the transaction
        TaxCalculationContext context = EuVatRuleEngine.Classify(
            sellerCountry: options.SellerCountryCode,
            buyerCountry: request.BuyerAddress.Country,
            buyerHasValidVat: buyerHasValidVat,
            ossEnabled: options.OssEnabled,
            ossCountries: options.OssRegisteredCountries);

        // Get the applicable rate
        decimal rate = 0m;
        string? jurisdiction = context.RateCountryCode;

        if (context.Exemption == TaxExemptionReason.None)
        {
            TaxRateEntry? rateEntry = await rateProvider
                .GetRateAsync(
                    context.RateCountryCode,
                    clock.Now,
                    request.BuyerPartyId,
                    cancellationToken)
                .ConfigureAwait(false);

            rate = rateEntry?.StandardRate ?? 0m;
        }

        // Calculate per-line tax
        var lineResults = new List<TaxLineResult>(request.LineItems.Count);
        decimal totalTax = 0m;

        foreach (TaxLineItem lineItem in request.LineItems)
        {
            decimal lineTax = Math.Round(lineItem.Amount * rate, 2, MidpointRounding.AwayFromZero);
            totalTax += lineTax;

            lineResults.Add(new TaxLineResult(
                TaxAmount: lineTax,
                TaxRate: rate,
                Jurisdiction: jurisdiction));
        }

        metrics.RecordCalculation(currentTenant.Id?.ToString(), Name);

        return new TaxResult(lineResults, totalTax, jurisdiction);
    }
}
