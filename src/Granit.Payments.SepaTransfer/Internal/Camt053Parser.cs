using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Granit.Payments.SepaTransfer.Domain;

namespace Granit.Payments.SepaTransfer.Internal;

/// <summary>
/// Parses ISO 20022 CAMT.053 (Bank-to-Customer Statement) XML files.
/// </summary>
internal sealed class Camt053Parser : IBankStatementParser
{
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:camt.053.001.08";

    private static readonly XmlReaderSettings SafeXmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        Async = true,
    };

    /// <inheritdoc/>
    public string Format => "camt053";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BankStatementEntry>> ParseAsync(
        Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = XmlReader.Create(stream, SafeXmlSettings);
        XDocument doc = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken)
            .ConfigureAwait(false);

        var entries = new List<BankStatementEntry>();

        // Navigate: Document > BkToCstmrStmt > Stmt > Ntry
        IEnumerable<XElement>? statements = doc.Root?.Descendants(Ns + "Ntry");

        if (statements is null)
        {
            // Try without namespace (some banks use different versions)
            statements = doc.Root?.Descendants("Ntry");
        }

        if (statements is null)
        {
            return entries;
        }

        foreach (XElement ntry in statements)
        {
            // Only credit entries (incoming payments)
            string? cdtDbtInd = ntry.Element(Ns + "CdtDbtInd")?.Value
                ?? ntry.Element("CdtDbtInd")?.Value;

            if (cdtDbtInd != "CRDT")
            {
                continue;
            }

            string? amountStr = ntry.Element(Ns + "Amt")?.Value
                ?? ntry.Element("Amt")?.Value;
            string? currency = ntry.Element(Ns + "Amt")?.Attribute("Ccy")?.Value
                ?? ntry.Element("Amt")?.Attribute("Ccy")?.Value;
            string? bookingDate = ntry.Element(Ns + "BookgDt")?.Element(Ns + "Dt")?.Value
                ?? ntry.Element("BookgDt")?.Element("Dt")?.Value;

            if (!decimal.TryParse(amountStr, CultureInfo.InvariantCulture, out decimal amount))
            {
                continue;
            }

            // Extract remittance information
            XElement? txDtls = ntry.Descendants(Ns + "TxDtls").FirstOrDefault()
                ?? ntry.Descendants("TxDtls").FirstOrDefault();

            string? structuredRef = txDtls?.Descendants(Ns + "Strd").FirstOrDefault()
                ?.Element(Ns + "CdtrRefInf")?.Element(Ns + "Ref")?.Value
                ?? txDtls?.Descendants("Strd").FirstOrDefault()
                ?.Element("CdtrRefInf")?.Element("Ref")?.Value;

            string? unstructuredRef = txDtls?.Descendants(Ns + "Ustrd").FirstOrDefault()?.Value
                ?? txDtls?.Descendants("Ustrd").FirstOrDefault()?.Value;

            string? debtorName = txDtls?.Descendants(Ns + "Dbtr").FirstOrDefault()
                ?.Element(Ns + "Nm")?.Value
                ?? txDtls?.Descendants("Dbtr").FirstOrDefault()?.Element("Nm")?.Value;

            string? debtorIban = txDtls?.Descendants(Ns + "DbtrAcct").FirstOrDefault()
                ?.Element(Ns + "Id")?.Element(Ns + "IBAN")?.Value
                ?? txDtls?.Descendants("DbtrAcct").FirstOrDefault()
                ?.Element("Id")?.Element("IBAN")?.Value;

            string? endToEndId = txDtls?.Descendants(Ns + "EndToEndId").FirstOrDefault()?.Value
                ?? txDtls?.Descendants("EndToEndId").FirstOrDefault()?.Value;

            entries.Add(new BankStatementEntry(
                BookingDate: DateTimeOffset.TryParse(bookingDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset dt) ? dt : DateTimeOffset.MinValue,
                Amount: amount,
                Currency: currency ?? "EUR",
                DebtorName: debtorName,
                DebtorIban: debtorIban,
                StructuredReference: structuredRef,
                UnstructuredReference: unstructuredRef,
                EndToEndId: endToEndId));
        }

        return entries;
    }
}
