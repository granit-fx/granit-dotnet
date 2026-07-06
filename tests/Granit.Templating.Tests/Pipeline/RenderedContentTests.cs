using Granit.Templating.Exceptions;
using Granit.Templating.Pipeline;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Pipeline;

public sealed class RenderedContentTests
{
    // -------------------------------------------------------------------------
    // BinaryRenderedContent
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // TextRenderedContent
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // RenderedTextResult
    // -------------------------------------------------------------------------

    [Fact]
    public void RenderedTextResult_WithOnlyHtml_DefaultsAreNull()
    {
        RenderedTextResult result = new(Html: "<p>Body</p>");

        result.Html.ShouldBe("<p>Body</p>");
        result.PlainText.ShouldBeNull();
        result.Subject.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // TemplateNotFoundException (culture branch)
    // -------------------------------------------------------------------------

    [Fact]
    public void TemplateNotFoundException_WithCulture_MessageContainsCultureInfo()
    {
        TemplateNotFoundException ex = new("Billing.Invoice", "fr-BE");

        ex.TemplateName.ShouldBe("Billing.Invoice");
        ex.Culture.ShouldBe("fr-BE");
        ex.Message.ShouldContain("fr-BE");
        ex.Message.ShouldContain("Billing.Invoice");
    }

    [Fact]
    public void TemplateNotFoundException_WithoutCulture_CulturePropertyIsNull()
    {
        TemplateNotFoundException ex = new("Billing.Invoice");

        ex.Culture.ShouldBeNull();
        ex.Message.ShouldContain("Billing.Invoice");
    }
}
