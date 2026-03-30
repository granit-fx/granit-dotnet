using Granit.Privacy.ProcessingPurposes;
using Granit.Privacy.ProcessingPurposes.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.ProcessingPurposes;

public sealed class ProcessingPurposeRegistryTests
{
    [Fact]
    public void GetAll_ReturnsAllRegisteredPurposes()
    {
        ProcessingPurposeRegistry sut = new(
        [
            new("marketing", "Marketing", "Send promo emails", "CONSENT", true),
            new("analytics", "Analytics", "Usage tracking", "LEGITIMATE_INTEREST", false),
        ]);

        sut.GetAll().Count.ShouldBe(2);
    }

    [Fact]
    public void GetPurpose_ExistingId_ReturnsPurpose()
    {
        ProcessingPurposeRegistry sut = new(
        [
            new("marketing", "Marketing", "Send promo emails", "CONSENT", true),
        ]);

        ProcessingPurposeDefinition? result = sut.GetPurpose("marketing");

        result.ShouldNotBeNull();
        result.PurposeId.ShouldBe("marketing");
        result.LegalBasis.ShouldBe("CONSENT");
    }

    [Fact]
    public void GetPurpose_CaseInsensitive()
    {
        ProcessingPurposeRegistry sut = new(
        [
            new("Marketing", "Marketing", "desc", "CONSENT", true),
        ]);

        sut.GetPurpose("MARKETING").ShouldNotBeNull();
        sut.GetPurpose("marketing").ShouldNotBeNull();
    }

    [Fact]
    public void GetPurpose_UnknownId_ReturnsNull()
    {
        ProcessingPurposeRegistry sut = new([]);

        sut.GetPurpose("nonexistent").ShouldBeNull();
    }

    [Fact]
    public void GetByLegalBasis_FiltersByBasis()
    {
        ProcessingPurposeRegistry sut = new(
        [
            new("marketing", "Marketing", "desc", "CONSENT", true),
            new("orders", "Orders", "desc", "CONTRACT", false),
            new("newsletter", "Newsletter", "desc", "CONSENT", true),
        ]);

        IReadOnlyList<ProcessingPurposeDefinition> result = sut.GetByLegalBasis("CONSENT");

        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.LegalBasis == "CONSENT");
    }

    [Fact]
    public void GetByLegalBasis_CaseInsensitive()
    {
        ProcessingPurposeRegistry sut = new(
        [
            new("marketing", "Marketing", "desc", "CONSENT", true),
        ]);

        sut.GetByLegalBasis("consent").Count.ShouldBe(1);
    }
}
