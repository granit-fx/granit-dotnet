using Granit.Invoicing;
using Granit.Invoicing.Dtos;
using Granit.Tax.Stripe.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Tax;

namespace Granit.Tax.Stripe.Internal;

/// <summary>Stripe Tax Calculations API implementation of <see cref="ITaxCalculator"/>.</summary>
internal sealed partial class StripeTaxCalculator(
    IStripeClient stripeClient,
    IOptions<StripeTaxOptions> options,
    ILogger<StripeTaxCalculator> logger) : ITaxCalculator
{
    /// <inheritdoc/>
    public string Name => "stripe-tax";

    /// <inheritdoc/>
    public async Task<TaxResult> CalculateAsync(
        TaxRequest request, CancellationToken cancellationToken = default)
    {
        var calcOptions = new CalculationCreateOptions
        {
            Currency = request.BuyerAddress.Country == "US" ? "usd" : "eur",
            CustomerDetails = new CalculationCustomerDetailsOptions
            {
                Address = new AddressOptions
                {
                    Country = request.BuyerAddress.Country,
                    City = request.BuyerAddress.City,
                    PostalCode = request.BuyerAddress.PostalCode,
                },
                AddressSource = "billing",
            },
            LineItems = request.LineItems.Select(li => new CalculationLineItemOptions
            {
                Amount = (long)(li.Amount * 100),
                TaxCode = li.TaxCode ?? options.Value.ProductTaxCode,
                Reference = li.Description,
            }).ToList(),
        };

        var service = new CalculationService(stripeClient);
        Calculation calc = await service.CreateAsync(calcOptions, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var lineResults = calc.LineItems.Data.Select(li => new TaxLineResult(
            TaxAmount: li.AmountTax / 100m,
            TaxRate: li.AmountTax > 0 && li.Amount > 0
                ? (decimal)li.AmountTax / li.Amount
                : 0m,
            Jurisdiction: null)).ToList();

        Log.CalculationCompleted(logger, calc.Id, calc.TaxAmountExclusive);

        return new TaxResult(
            lineResults,
            TotalTax: calc.TaxAmountExclusive / 100m,
            Jurisdiction: null);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe Tax calculation completed: {CalculationId}, tax = {TaxAmount}")]
        public static partial void CalculationCompleted(ILogger logger, string calculationId, long taxAmount);
    }
}
