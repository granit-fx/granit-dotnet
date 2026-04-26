using Granit.Payments.SepaTransfer.Internal;
using Granit.Payments.SepaTransfer.Options;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Payments.SepaTransfer.Tests.Internal;

public sealed class StructuredReferenceGeneratorTests
{
    private static StructuredReferenceGenerator Build(string prefix = "GRN") =>
        new(MsOptions.Create(new SepaTransferOptions { ReferencePrefix = prefix }));

    [Fact]
    public void Generate_FormatsAsPrefixDashHexUpper16Chars()
    {
        StructuredReferenceGenerator gen = Build("GRN");
        var txId = Guid.Parse("a1b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6");

        string result = gen.Generate(txId);

        result.ShouldStartWith("GRN-");
        // Format: GRN-{16 hex chars upper}
        result.Length.ShouldBe(20);
        result[4..].ShouldMatch("^[0-9A-F]{16}$");
    }

    [Fact]
    public void Generate_DifferentPrefix_AppliedToOutput()
    {
        StructuredReferenceGenerator gen = Build("INV");

        string result = gen.Generate(Guid.NewGuid());

        result.ShouldStartWith("INV-");
    }

    [Fact]
    public void ExtractPrefix_ValidReference_ReturnsTransactionPart()
    {
        StructuredReferenceGenerator gen = Build("GRN");

        string? extracted = gen.ExtractPrefix("GRN-A1B2C3D4E5F6A7B8");

        extracted.ShouldBe("A1B2C3D4E5F6A7B8");
    }

    [Fact]
    public void ExtractPrefix_CaseInsensitivePrefix_StillExtracts()
    {
        StructuredReferenceGenerator gen = Build("GRN");

        string? extracted = gen.ExtractPrefix("grn-A1B2C3D4");

        extracted.ShouldBe("A1B2C3D4");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractPrefix_BlankInput_ReturnsNull(string? input)
    {
        StructuredReferenceGenerator gen = Build();

        gen.ExtractPrefix(input).ShouldBeNull();
    }

    [Fact]
    public void ExtractPrefix_WrongPrefix_ReturnsNull()
    {
        StructuredReferenceGenerator gen = Build("GRN");

        gen.ExtractPrefix("INV-A1B2").ShouldBeNull();
    }

    [Fact]
    public void RoundTrip_GenerateThenExtract_PreservesPrefix()
    {
        StructuredReferenceGenerator gen = Build("GRN");
        var txId = Guid.NewGuid();

        string reference = gen.Generate(txId);
        string? extracted = gen.ExtractPrefix(reference);

        extracted.ShouldNotBeNull();
        extracted.Length.ShouldBe(16);
    }
}
