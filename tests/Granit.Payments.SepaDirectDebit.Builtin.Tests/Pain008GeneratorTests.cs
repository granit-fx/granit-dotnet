using System.Text;
using System.Xml.Linq;
using Granit.Payments.SepaDirectDebit.Builtin.Internal;
using Granit.Payments.SepaDirectDebit.Builtin.Options;
using Granit.Payments.SepaDirectDebit.Contracts;
using Granit.Payments.SepaDirectDebit.Domain;
using Granit.Timing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Payments.SepaDirectDebit.Builtin.Tests;

public sealed class Pain008GeneratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 15, 9, 30, 0, TimeSpan.Zero);
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.08";

    private static SepaDirectDebitBuiltinOptions DefaultOptions(SddScheme scheme = SddScheme.Core) => new()
    {
        CreditorIban = "BE68539007547034",
        CreditorBic = "GKCCBEBB",
        CreditorName = "Acme Corp",
        CreditorId = "BE12ZZZ0000012345",
        DefaultScheme = scheme,
    };

    private static (Mandate Mandate, DirectDebitPayment Collection) BuildPair(
        string mandateRef = "SDD-A1B2",
        decimal amount = 100m,
        string? bic = "BNPABEBB")
    {
        var mandate = Mandate.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            mandateRef, SddScheme.Core,
            "John Doe", "BE53000000000087", "BE12ZZZ0000012345",
            debtorBic: bic);
        mandate.Activate(Now.AddDays(-30));

        var collection = DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), amount, "EUR", Now);
        mandate.AddCollection(collection);
        return (mandate, collection);
    }

    private static Pain008Generator Build(SepaDirectDebitBuiltinOptions opts)
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        return new Pain008Generator(Microsoft.Extensions.Options.Options.Create(opts), clock);
    }

    [Fact]
    public async Task GenerateAsync_EmittedFile_HasCorrectMetadata()
    {
        Pain008Generator gen = Build(DefaultOptions());
        (Mandate mandate, DirectDebitPayment collection) = BuildPair(amount: 50.25m);

        CollectionFileResult result = await gen.GenerateAsync(
            [collection], [mandate], TestContext.Current.CancellationToken);

        result.CollectionCount.ShouldBe(1);
        result.TotalAmount.ShouldBe(50.25m);
        result.FileName.ShouldStartWith("SDD-20260415-");
        result.FileName.ShouldEndWith(".xml");
        result.Content.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_XmlIncludesGroupHeaderAndCreditorInfo()
    {
        Pain008Generator gen = Build(DefaultOptions());
        (Mandate mandate, DirectDebitPayment collection) = BuildPair(amount: 100m);

        CollectionFileResult result = await gen.GenerateAsync(
            [collection], [mandate], TestContext.Current.CancellationToken);

        var doc = XDocument.Load(new MemoryStream(result.Content));
        XElement? grpHdr = doc.Descendants(Ns + "GrpHdr").FirstOrDefault();
        grpHdr.ShouldNotBeNull();
        grpHdr.Element(Ns + "MsgId")?.Value.ShouldBe("SDD-20260415093000");
        grpHdr.Element(Ns + "NbOfTxs")?.Value.ShouldBe("1");
        grpHdr.Element(Ns + "CtrlSum")?.Value.ShouldBe("100.00");
        grpHdr.Descendants(Ns + "Nm").First().Value.ShouldBe("Acme Corp");
    }

    [Fact]
    public async Task GenerateAsync_AlwaysEmitsRcurSequenceType()
    {
        Pain008Generator gen = Build(DefaultOptions());
        (Mandate mandate, DirectDebitPayment collection) = BuildPair();

        CollectionFileResult result = await gen.GenerateAsync(
            [collection], [mandate], TestContext.Current.CancellationToken);

        var doc = XDocument.Load(new MemoryStream(result.Content));
        doc.Descendants(Ns + "SeqTp").First().Value.ShouldBe("RCUR");
    }

    [Fact]
    public async Task GenerateAsync_B2BScheme_EmitsB2BLocalInstrument()
    {
        Pain008Generator gen = Build(DefaultOptions(SddScheme.B2B));
        (Mandate mandate, DirectDebitPayment collection) = BuildPair();

        CollectionFileResult result = await gen.GenerateAsync(
            [collection], [mandate], TestContext.Current.CancellationToken);

        var doc = XDocument.Load(new MemoryStream(result.Content));
        doc.Descendants(Ns + "LclInstrm")
            .Descendants(Ns + "Cd").First().Value.ShouldBe("B2B");
    }

    [Fact]
    public async Task GenerateAsync_CoreScheme_EmitsCoreLocalInstrument()
    {
        Pain008Generator gen = Build(DefaultOptions(SddScheme.Core));
        (Mandate mandate, DirectDebitPayment collection) = BuildPair();

        CollectionFileResult result = await gen.GenerateAsync(
            [collection], [mandate], TestContext.Current.CancellationToken);

        var doc = XDocument.Load(new MemoryStream(result.Content));
        doc.Descendants(Ns + "LclInstrm")
            .Descendants(Ns + "Cd").First().Value.ShouldBe("CORE");
    }

    [Fact]
    public async Task GenerateAsync_NoBic_EmitsNotProvidedFallback()
    {
        Pain008Generator gen = Build(DefaultOptions());
        (Mandate mandate, DirectDebitPayment collection) = BuildPair(bic: null);

        CollectionFileResult result = await gen.GenerateAsync(
            [collection], [mandate], TestContext.Current.CancellationToken);

        var doc = XDocument.Load(new MemoryStream(result.Content));
        IEnumerable<XElement> debtorAgt = doc.Descendants(Ns + "DbtrAgt");
        debtorAgt.Descendants(Ns + "Id")
            .Where(e => e.Value == "NOTPROVIDED")
            .ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_CollectionWithoutMandate_IsSkipped()
    {
        Pain008Generator gen = Build(DefaultOptions());
        (Mandate mandate, DirectDebitPayment paired) = BuildPair();
        var orphan = DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), 999m, "EUR", Now);

        CollectionFileResult result = await gen.GenerateAsync(
            [paired, orphan], [mandate], TestContext.Current.CancellationToken);

        var doc = XDocument.Load(new MemoryStream(result.Content));
        doc.Descendants(Ns + "DrctDbtTxInf").Count().ShouldBe(1);
    }

    [Fact]
    public async Task GenerateAsync_TotalAmount_SumsAllCollections()
    {
        Pain008Generator gen = Build(DefaultOptions());
        (Mandate m1, DirectDebitPayment c1) = BuildPair(mandateRef: "SDD-1", amount: 50m);
        (Mandate m2, DirectDebitPayment c2) = BuildPair(mandateRef: "SDD-2", amount: 75.50m);

        CollectionFileResult result = await gen.GenerateAsync(
            [c1, c2], [m1, m2], TestContext.Current.CancellationToken);

        result.TotalAmount.ShouldBe(125.50m);
        result.CollectionCount.ShouldBe(2);
    }
}
