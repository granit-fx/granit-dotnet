using Granit.Templating.Keys;
using Granit.Templating.Mjml.Internal;
using Shouldly;
using Xunit;

namespace Granit.Templating.Mjml.Tests;

public sealed class MjmlTransformerTests
{
    private readonly MjmlTransformer _sut = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void Order_Is100() =>
        _sut.Order.ShouldBe(100);

    [Fact]
    public void CanTransform_Html_ReturnsTrue() =>
        _sut.CanTransform(DocumentFormat.Html).ShouldBeTrue();

    [Fact]
    public void CanTransform_Pdf_ReturnsFalse() =>
        _sut.CanTransform(DocumentFormat.Pdf).ShouldBeFalse();

    [Fact]
    public void CanTransform_Excel_ReturnsFalse() =>
        _sut.CanTransform(DocumentFormat.Excel).ShouldBeFalse();

    [Fact]
    public async Task PlainHtml_PassesThrough()
    {
        const string html = "<p>Hello world</p>";

        string result = await _sut.TransformAsync(html, DocumentFormat.Html, CancellationToken);

        result.ShouldBe(html);
    }

    [Fact]
    public async Task PlainHtml_WithWhitespace_PassesThrough()
    {
        const string html = "  \n  <html><body><p>Hello</p></body></html>";

        string result = await _sut.TransformAsync(html, DocumentFormat.Html, CancellationToken);

        result.ShouldBe(html);
    }

    [Fact]
    public async Task Mjml_ProducesTableBasedHtml()
    {
        const string mjml = """
            <mjml>
              <mj-body>
                <mj-section>
                  <mj-column>
                    <mj-text>Hello World</mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        string result = await _sut.TransformAsync(mjml, DocumentFormat.Html, CancellationToken);

        result.ShouldNotContain("<mjml");
        result.ShouldNotContain("<mj-");
        result.ShouldContain("<table");
        result.ShouldContain("Hello World");
        result.ShouldContain("<!doctype html>");
    }

    [Fact]
    public async Task Mjml_ProducesInlineCss()
    {
        const string mjml = """
            <mjml>
              <mj-body>
                <mj-section background-color="#ff0000">
                  <mj-column>
                    <mj-text color="#ffffff">Styled</mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        string result = await _sut.TransformAsync(mjml, DocumentFormat.Html, CancellationToken);

        result.ShouldContain("style=");
        result.ShouldContain("Styled");
    }

    [Fact]
    public async Task Mjml_ProducesMsoConditionals()
    {
        const string mjml = """
            <mjml>
              <mj-body>
                <mj-section>
                  <mj-column>
                    <mj-text>Outlook test</mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        string result = await _sut.TransformAsync(mjml, DocumentFormat.Html, CancellationToken);

        result.ShouldContain("<!--[if mso");
    }

    [Fact]
    public async Task Mjml_PreservesScribanVariables()
    {
        const string mjml = """
            <mjml>
              <mj-body>
                <mj-section>
                  <mj-column>
                    <mj-text>Hello {{ model.name }}, welcome!</mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        string result = await _sut.TransformAsync(mjml, DocumentFormat.Html, CancellationToken);

        result.ShouldContain("{{ model.name }}");
    }

    [Fact]
    public async Task Mjml_WithButton_ProducesAccessibleLink()
    {
        const string mjml = """
            <mjml>
              <mj-body>
                <mj-section>
                  <mj-column>
                    <mj-button href="https://example.com">Click me</mj-button>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        string result = await _sut.TransformAsync(mjml, DocumentFormat.Html, CancellationToken);

        result.ShouldContain("https://example.com");
        result.ShouldContain("Click me");
    }

    [Fact]
    public async Task Mjml_WithTable_RendersRows()
    {
        const string mjml = """
            <mjml>
              <mj-body>
                <mj-section>
                  <mj-column>
                    <mj-table>
                      <tr><td>Name</td><td><strong>example</strong></td></tr>
                      <tr><td>Reference</td><td><code>abc-123</code></td></tr>
                    </mj-table>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        string result = await _sut.TransformAsync(mjml, DocumentFormat.Html, CancellationToken);

        result.ShouldContain("example");
        result.ShouldContain("abc-123");
        result.ShouldContain("<table");
    }

    [Fact]
    public async Task Mjml_CaseInsensitive_DetectsMjmlTag()
    {
        const string mjml = """
            <MJML>
              <mj-body>
                <mj-section>
                  <mj-column>
                    <mj-text>Case test</mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </MJML>
            """;

        string result = await _sut.TransformAsync(mjml, DocumentFormat.Html, CancellationToken);

        result.ShouldContain("Case test");
        result.ShouldContain("<table");
    }
}
