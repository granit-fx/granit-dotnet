using Granit.ReferenceData.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests;

public sealed class ReferenceDataUpdateRequestTests
{
    [Fact]
    public void LabelEn_Is_Required()
    {
        ReferenceDataUpdateRequest request = new("Belgium");

        request.LabelEn.ShouldBe("Belgium");
    }

    [Fact]
    public void Default_OptionalLabels_Are_Empty()
    {
        ReferenceDataUpdateRequest request = new("Belgium");

        request.LabelFr.ShouldBe(string.Empty);
        request.LabelNl.ShouldBe(string.Empty);
        request.LabelDe.ShouldBe(string.Empty);
        request.LabelEs.ShouldBe(string.Empty);
        request.LabelIt.ShouldBe(string.Empty);
        request.LabelPt.ShouldBe(string.Empty);
        request.LabelZh.ShouldBe(string.Empty);
        request.LabelJa.ShouldBe(string.Empty);
        request.LabelPl.ShouldBe(string.Empty);
        request.LabelTr.ShouldBe(string.Empty);
        request.LabelKo.ShouldBe(string.Empty);
        request.LabelSv.ShouldBe(string.Empty);
        request.LabelCs.ShouldBe(string.Empty);
    }

    [Fact]
    public void Default_SortOrder_Is_Zero()
    {
        ReferenceDataUpdateRequest request = new("Belgium");

        request.SortOrder.ShouldBe(0);
    }

    [Fact]
    public void Default_IsActive_Is_True()
    {
        ReferenceDataUpdateRequest request = new("Belgium");

        request.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Default_ValidFrom_Is_Null()
    {
        ReferenceDataUpdateRequest request = new("Belgium");

        request.ValidFrom.ShouldBeNull();
    }

    [Fact]
    public void Default_ValidTo_Is_Null()
    {
        ReferenceDataUpdateRequest request = new("Belgium");

        request.ValidTo.ShouldBeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ReferenceDataUpdateRequest request = new(
            "Belgium",
            LabelFr: "Belgique",
            LabelNl: "België",
            LabelDe: "Belgien",
            LabelEs: "Bélgica",
            LabelIt: "Belgio",
            LabelPt: "Bélgica",
            LabelZh: "比利时",
            LabelJa: "ベルギー",
            LabelPl: "Belgia",
            LabelTr: "Belçika",
            LabelKo: "벨기에",
            LabelSv: "Belgien",
            LabelCs: "Belgie",
            SortOrder: 10,
            IsActive: false,
            ValidFrom: now,
            ValidTo: now.AddYears(1));

        request.LabelFr.ShouldBe("Belgique");
        request.LabelCs.ShouldBe("Belgie");
        request.SortOrder.ShouldBe(10);
        request.IsActive.ShouldBeFalse();
        request.ValidFrom.ShouldBe(now);
        request.ValidTo.ShouldBe(now.AddYears(1));
    }
}
