using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Granit.Payments.SepaDirectDebit.Contracts;
using Granit.Payments.SepaDirectDebit.Domain;
using Granit.Payments.SepaDirectDebit.Internal.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.Payments.SepaDirectDebit.Internal.Internal;

/// <summary>
/// Generates ISO 20022 PAIN.008.001.08 XML files for SEPA Direct Debit collections.
/// </summary>
/// <remarks>
/// <para>Sequence type simplified per post-2016 SDD Core Rulebook:</para>
/// <list type="bullet">
/// <item><c>RCUR</c> for all recurring collections (FRST is obsolete)</item>
/// <item><c>OOFF</c> for one-shot collections</item>
/// </list>
/// </remarks>
internal sealed class Pain008Generator(
    IOptions<SepaDirectDebitInternalOptions> options,
    IClock clock) : ICollectionFileGenerator
{
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.08";

    /// <inheritdoc/>
    public Task<CollectionFileResult> GenerateAsync(
        IReadOnlyList<DirectDebitPayment> collections,
        IReadOnlyList<Mandate> mandates,
        CancellationToken cancellationToken = default)
    {
        SepaDirectDebitInternalOptions config = options.Value;
        DateTimeOffset now = clock.Now;

        string msgId = $"SDD-{now:yyyyMMddHHmmss}";

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ns + "Document",
                new XElement(Ns + "CstmrDrctDbtInitn",
                    BuildGroupHeader(msgId, config, collections.Count,
                        collections.Sum(c => c.Amount)),
                    BuildPaymentInfo(collections, mandates, config, now))));

        using var ms = new MemoryStream();
        doc.Save(ms);
        byte[] content = ms.ToArray();

        string fileName = $"SDD-{now:yyyyMMdd}-{now:HHmmss}.xml";

        return Task.FromResult(new CollectionFileResult(
            Content: content,
            FileName: fileName,
            CollectionCount: collections.Count,
            TotalAmount: collections.Sum(c => c.Amount)));
    }

    private XElement BuildGroupHeader(
        string msgId, SepaDirectDebitInternalOptions config,
        int txCount, decimal totalAmount)
    {
        return new XElement(Ns + "GrpHdr",
            new XElement(Ns + "MsgId", msgId),
            new XElement(Ns + "CreDtTm", clock.Now.ToString("o")),
            new XElement(Ns + "NbOfTxs", txCount),
            new XElement(Ns + "CtrlSum", totalAmount.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Ns + "InitgPty",
                new XElement(Ns + "Nm", config.CreditorName)));
    }

    private static XElement BuildPaymentInfo(
        IReadOnlyList<DirectDebitPayment> collections,
        IReadOnlyList<Mandate> mandates,
        SepaDirectDebitInternalOptions config,
        DateTimeOffset requestedDate)
    {
        var pmtInf = new XElement(Ns + "PmtInf",
            new XElement(Ns + "PmtInfId", $"PMT-{requestedDate:yyyyMMdd}"),
            new XElement(Ns + "PmtMtd", "DD"),
            new XElement(Ns + "NbOfTxs", collections.Count),
            new XElement(Ns + "CtrlSum", collections.Sum(c => c.Amount).ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Ns + "PmtTpInf",
                new XElement(Ns + "SvcLvl", new XElement(Ns + "Cd", "SEPA")),
                new XElement(Ns + "LclInstrm", new XElement(Ns + "Cd", config.DefaultScheme == SddScheme.B2B ? "B2B" : "CORE")),
                new XElement(Ns + "SeqTp", "RCUR")), // Always RCUR (post-2016)
            new XElement(Ns + "ReqdColltnDt", requestedDate.ToString("yyyy-MM-dd")),
            new XElement(Ns + "Cdtr", new XElement(Ns + "Nm", config.CreditorName)),
            new XElement(Ns + "CdtrAcct",
                new XElement(Ns + "Id", new XElement(Ns + "IBAN", config.CreditorIban))),
            new XElement(Ns + "CdtrAgt",
                new XElement(Ns + "FinInstnId", new XElement(Ns + "BICFI", config.CreditorBic))),
            new XElement(Ns + "CdtrSchmeId",
                new XElement(Ns + "Id",
                    new XElement(Ns + "PrvtId",
                        new XElement(Ns + "Othr",
                            new XElement(Ns + "Id", config.CreditorId),
                            new XElement(Ns + "SchmeNm",
                                new XElement(Ns + "Prtry", "SEPA")))))));

        foreach (DirectDebitPayment collection in collections)
        {
            // Find the mandate for this collection (via parent navigation)
            Mandate? mandate = mandates.FirstOrDefault(m =>
                m.Collections.Any(c => c.Id == collection.Id));

            if (mandate is null)
            {
                continue;
            }

            pmtInf.Add(new XElement(Ns + "DrctDbtTxInf",
                new XElement(Ns + "PmtId",
                    new XElement(Ns + "EndToEndId", collection.Id.ToString("N")[..16])),
                new XElement(Ns + "InstdAmt",
                    new XAttribute("Ccy", collection.Currency),
                    collection.Amount.ToString("F2", CultureInfo.InvariantCulture)),
                new XElement(Ns + "DrctDbtTx",
                    new XElement(Ns + "MndtRltdInf",
                        new XElement(Ns + "MndtId", mandate.MandateReference),
                        new XElement(Ns + "DtOfSgntr", mandate.SignedAt?.ToString("yyyy-MM-dd") ?? "1970-01-01"))),
                new XElement(Ns + "DbtrAgt",
                    new XElement(Ns + "FinInstnId",
                        mandate.DebtorBic is not null
                            ? new XElement(Ns + "BICFI", mandate.DebtorBic)
                            : new XElement(Ns + "Othr", new XElement(Ns + "Id", "NOTPROVIDED")))),
                new XElement(Ns + "Dbtr", new XElement(Ns + "Nm", mandate.DebtorName)),
                new XElement(Ns + "DbtrAcct",
                    new XElement(Ns + "Id", new XElement(Ns + "IBAN", mandate.DebtorIban)))));
        }

        return pmtInf;
    }
}
