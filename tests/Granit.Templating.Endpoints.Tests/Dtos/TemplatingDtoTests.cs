using Granit.Templating.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Templating.Endpoints.Tests.Dtos;

public sealed class TemplatingDtoTests
{
    // -------------------------------------------------------------------------
    // SaveTemplateRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void SaveTemplateRequest_DefaultMimeType_IsTextHtml()
    {
        SaveTemplateRequest request = new(Content: "<p>Hello</p>", Name: "Test.Template", Culture: null);

        request.MimeType.ShouldBe("text/html");
    }

    [Fact]
    public void SaveTemplateRequest_AllProperties_SetCorrectly()
    {
        SaveTemplateRequest request = new(Content: "<p>Facture</p>", Name: "Billing.Invoice", Culture: "fr-BE", MimeType: "text/plain");

        request.Name.ShouldBe("Billing.Invoice");
        request.Culture.ShouldBe("fr-BE");
        request.Content.ShouldBe("<p>Facture</p>");
        request.MimeType.ShouldBe("text/plain");
    }

    // -------------------------------------------------------------------------
    // SaveTemplateCategoryRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void SaveTemplateCategoryRequest_DefaultSortOrder_IsZero()
    {
        SaveTemplateCategoryRequest request = new("Invoices", null, null);

        request.SortOrder.ShouldBe(0);
    }

    [Fact]
    public void SaveTemplateCategoryRequest_AllProperties_SetCorrectly()
    {
        SaveTemplateCategoryRequest request = new("Reports", "Quarterly reports", "bar-chart", 5);

        request.Name.ShouldBe("Reports");
        request.Description.ShouldBe("Quarterly reports");
        request.Icon.ShouldBe("bar-chart");
        request.SortOrder.ShouldBe(5);
    }

    // -------------------------------------------------------------------------
    // TemplateCategoryResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void TemplateCategoryResponse_AllProperties_SetCorrectly()
    {
        var id = Guid.NewGuid();
        TemplateCategoryResponse response = new(id, "Letters", "Patient letters", "file-text", 1, 5);

        response.Id.ShouldBe(id);
        response.Name.ShouldBe("Letters");
        response.Description.ShouldBe("Patient letters");
        response.Icon.ShouldBe("file-text");
        response.SortOrder.ShouldBe(1);
        response.TemplateCount.ShouldBe(5);
    }

    // -------------------------------------------------------------------------
    // TemplateDetailResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void TemplateDetailResponse_WithNullDraftAndPublished_SetsCorrectly()
    {
        TemplateDetailResponse response = new("Billing.Invoice", "fr", null, null, null);

        response.Name.ShouldBe("Billing.Invoice");
        response.Culture.ShouldBe("fr");
        response.Draft.ShouldBeNull();
        response.Published.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // TemplatePreviewRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void TemplatePreviewRequest_NullCultureAndData()
    {
        TemplatePreviewRequest request = new(null, null);

        request.Culture.ShouldBeNull();
        request.Data.ShouldBeNull();
    }
}
