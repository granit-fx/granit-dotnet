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

    // -------------------------------------------------------------------------
    // SaveTemplateCategoryRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void SaveTemplateCategoryRequest_DefaultSortOrder_IsZero()
    {
        SaveTemplateCategoryRequest request = new("Invoices", null, null);

        request.SortOrder.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // TemplateCategoryResponse
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // TemplateDetailResponse
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // TemplatePreviewRequest
    // -------------------------------------------------------------------------

}
