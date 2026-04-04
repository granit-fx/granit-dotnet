using Granit.Payments.SepaTransfer.Options;
using Microsoft.Extensions.Options;

namespace Granit.Payments.SepaTransfer.Internal;

/// <summary>
/// Generates structured payment references that link transfers to invoices.
/// </summary>
/// <remarks>
/// Format: <c>{Prefix}-{TransactionId:N16}</c> (e.g., "GRN-A1B2C3D4E5F6A7B8").
/// The reference is included in the bank transfer instructions and used
/// for reconciliation matching.
/// </remarks>
internal sealed class StructuredReferenceGenerator(
    IOptions<SepaTransferOptions> options)
{
    /// <summary>Generates a structured reference for a transaction.</summary>
    public string Generate(Guid transactionId) =>
        $"{options.Value.ReferencePrefix}-{transactionId.ToString("N")[..16].ToUpperInvariant()}";

    /// <summary>Extracts the transaction ID prefix from a structured reference.</summary>
    public string? ExtractPrefix(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        string expectedPrefix = $"{options.Value.ReferencePrefix}-";
        if (reference.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return reference[expectedPrefix.Length..];
        }

        return null;
    }
}
