using System.Text;
using Granit.Payments.SepaTransfer.Domain;
using Granit.Payments.SepaTransfer.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.SepaTransfer.Tests.Internal;

public sealed class Camt053ParserTests
{
    private const string Ns = "urn:iso:std:iso:20022:tech:xsd:camt.053.001.08";

    private static MemoryStream Stream(string xml) => new(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public async Task ParseAsync_NamespacedCreditEntry_Extracts()
    {
        string xml = $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Document xmlns="{Ns}">
          <BkToCstmrStmt>
            <Stmt>
              <Ntry>
                <Amt Ccy="EUR">125.50</Amt>
                <CdtDbtInd>CRDT</CdtDbtInd>
                <BookgDt><Dt>2026-04-15</Dt></BookgDt>
                <NtryDtls><TxDtls>
                  <RmtInf>
                    <Strd><CdtrRefInf><Ref>GRN-ABC123</Ref></CdtrRefInf></Strd>
                    <Ustrd>Invoice payment</Ustrd>
                  </RmtInf>
                  <RltdPties>
                    <Dbtr><Nm>John Doe</Nm></Dbtr>
                    <DbtrAcct><Id><IBAN>BE53000000000087</IBAN></Id></DbtrAcct>
                  </RltdPties>
                  <Refs><EndToEndId>E2E-99</EndToEndId></Refs>
                </TxDtls></NtryDtls>
              </Ntry>
            </Stmt>
          </BkToCstmrStmt>
        </Document>
        """;
        var parser = new Camt053Parser();

        IReadOnlyList<BankStatementEntry> entries = await parser.ParseAsync(
            Stream(xml), TestContext.Current.CancellationToken);

        entries.Count.ShouldBe(1);
        entries[0].Amount.ShouldBe(125.50m);
        entries[0].Currency.ShouldBe("EUR");
        entries[0].DebtorName.ShouldBe("John Doe");
        entries[0].DebtorIban.ShouldBe("BE53000000000087");
        entries[0].StructuredReference.ShouldBe("GRN-ABC123");
        entries[0].UnstructuredReference.ShouldBe("Invoice payment");
        entries[0].EndToEndId.ShouldBe("E2E-99");
        entries[0].BookingDate.Year.ShouldBe(2026);
    }

    [Fact]
    public async Task ParseAsync_DebitEntry_IsSkipped()
    {
        string xml = $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Document xmlns="{Ns}">
          <BkToCstmrStmt><Stmt>
            <Ntry>
              <Amt Ccy="EUR">100.00</Amt>
              <CdtDbtInd>DBIT</CdtDbtInd>
            </Ntry>
          </Stmt></BkToCstmrStmt>
        </Document>
        """;
        var parser = new Camt053Parser();

        IReadOnlyList<BankStatementEntry> entries = await parser.ParseAsync(
            Stream(xml), TestContext.Current.CancellationToken);

        entries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ParseAsync_InvalidAmount_IsSkipped()
    {
        string xml = $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Document xmlns="{Ns}">
          <BkToCstmrStmt><Stmt>
            <Ntry>
              <Amt Ccy="EUR">not-a-number</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
            </Ntry>
          </Stmt></BkToCstmrStmt>
        </Document>
        """;
        var parser = new Camt053Parser();

        IReadOnlyList<BankStatementEntry> entries = await parser.ParseAsync(
            Stream(xml), TestContext.Current.CancellationToken);

        entries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ParseAsync_EmptyDocument_ReturnsEmptyList()
    {
        string xml = $"""<?xml version="1.0"?><Document xmlns="{Ns}"></Document>""";
        var parser = new Camt053Parser();

        IReadOnlyList<BankStatementEntry> entries = await parser.ParseAsync(
            Stream(xml), TestContext.Current.CancellationToken);

        entries.Count.ShouldBe(0);
    }

    [Fact]
    public void Format_IsCamt053() =>
        new Camt053Parser().Format.ShouldBe("camt053");
}
