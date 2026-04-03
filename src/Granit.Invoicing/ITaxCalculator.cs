using Granit.Invoicing.Domain;
using Granit.Invoicing.Dtos;

namespace Granit.Invoicing;

/// <summary>Tax calculation provider (EU VAT, Stripe Tax, custom).</summary>
public interface ITaxCalculator
{
    /// <summary>Provider name (e.g., "eu-vat", "stripe-tax").</summary>
    string Name { get; }

    /// <summary>Calculates tax for a set of line items and addresses.</summary>
    Task<TaxResult> CalculateAsync(TaxRequest request, CancellationToken cancellationToken = default);
}
