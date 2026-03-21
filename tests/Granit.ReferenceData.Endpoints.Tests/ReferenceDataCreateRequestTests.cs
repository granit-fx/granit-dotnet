using Granit.ReferenceData.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests;

public sealed class ReferenceDataCreateRequestTests
{
    [Fact]
    public void Code_And_LabelEn_Are_Required()
    {
        ReferenceDataCreateRequest request = new("EUR", "Euro");

        request.Code.ShouldBe("EUR");
        request.LabelEn.ShouldBe("Euro");
    }

    [Fact]
    public void Default_OptionalLabels_Are_Empty()
    {
        ReferenceDataCreateRequest request = new("EUR", "Euro");

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
        ReferenceDataCreateRequest request = new("EUR", "Euro");

        request.SortOrder.ShouldBe(0);
    }

    [Fact]
    public void Default_ValidFrom_Is_Null()
    {
        ReferenceDataCreateRequest request = new("EUR", "Euro");

        request.ValidFrom.ShouldBeNull();
    }

    [Fact]
    public void Default_ValidTo_Is_Null()
    {
        ReferenceDataCreateRequest request = new("EUR", "Euro");

        request.ValidTo.ShouldBeNull();
    }

    [Fact]
    public void AllLabels_CanBeSet()
    {
        ReferenceDataCreateRequest request = new(
            "EUR", "Euro",
            LabelFr: "Euro-FR",
            LabelNl: "Euro-NL",
            LabelDe: "Euro-DE",
            LabelEs: "Euro-ES",
            LabelIt: "Euro-IT",
            LabelPt: "Euro-PT",
            LabelZh: "Euro-ZH",
            LabelJa: "Euro-JA",
            LabelPl: "Euro-PL",
            LabelTr: "Euro-TR",
            LabelKo: "Euro-KO",
            LabelSv: "Euro-SV",
            LabelCs: "Euro-CS",
            SortOrder: 5,
            ValidFrom: DateTimeOffset.UtcNow,
            ValidTo: DateTimeOffset.UtcNow.AddYears(1));

        request.LabelFr.ShouldBe("Euro-FR");
        request.LabelZh.ShouldBe("Euro-ZH");
        request.LabelCs.ShouldBe("Euro-CS");
        request.SortOrder.ShouldBe(5);
        request.ValidFrom.ShouldNotBeNull();
        request.ValidTo.ShouldNotBeNull();
    }
}
