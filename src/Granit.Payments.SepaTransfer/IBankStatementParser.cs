using Granit.Payments.SepaTransfer.Domain;

namespace Granit.Payments.SepaTransfer;

/// <summary>
/// Parses bank statement files into structured entries.
/// </summary>
public interface IBankStatementParser
{
    /// <summary>Supported file format (e.g., "camt053", "mt940", "csv").</summary>
    string Format { get; }

    /// <summary>Parses a bank statement stream into entries.</summary>
    Task<IReadOnlyList<BankStatementEntry>> ParseAsync(
        Stream stream, CancellationToken cancellationToken = default);
}
