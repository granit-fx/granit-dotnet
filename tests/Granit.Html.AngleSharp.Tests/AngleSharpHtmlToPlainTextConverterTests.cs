using Shouldly;
using Xunit;

namespace Granit.Html.AngleSharp.Tests;

public sealed class AngleSharpHtmlToPlainTextConverterTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static readonly AngleSharpHtmlToPlainTextConverter Converter = new();

    [Fact]
    public async Task EmptyString_ReturnsEmpty()
    {
        string result = await Converter.ConvertAsync("", CancellationToken);
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task NullString_ReturnsEmpty()
    {
        string result = await Converter.ConvertAsync(null!, CancellationToken);
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhitespaceOnly_ReturnsEmpty()
    {
        string result = await Converter.ConvertAsync("   \n\t  ", CancellationToken);
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Paragraph_ExtractsText()
    {
        string result = await Converter.ConvertAsync("<p>Hello world</p>", CancellationToken);
        result.ShouldBe("Hello world");
    }

    [Fact]
    public async Task MultipleParagraphs_SeparatedByDoubleNewline()
    {
        string result = await Converter.ConvertAsync(
            "<p>First paragraph</p><p>Second paragraph</p>", CancellationToken);
        result.ShouldBe("First paragraph\n\nSecond paragraph");
    }

    [Fact]
    public async Task LineBreak_ProducesNewline()
    {
        string result = await Converter.ConvertAsync(
            "<p>Line one<br>Line two</p>", CancellationToken);
        result.ShouldBe("Line one\nLine two");
    }

    [Fact]
    public async Task Heading_ConvertsToUppercase()
    {
        string result = await Converter.ConvertAsync(
            "<h1>Welcome</h1><p>Content here</p>", CancellationToken);
        result.ShouldBe("WELCOME\n\nContent here");
    }

    [Fact]
    public async Task AllHeadingLevels_ConvertToUppercase()
    {
        string result = await Converter.ConvertAsync(
            "<h1>One</h1><h2>Two</h2><h3>Three</h3><h4>Four</h4><h5>Five</h5><h6>Six</h6>", CancellationToken);
        result.ShouldContain("ONE");
        result.ShouldContain("TWO");
        result.ShouldContain("THREE");
        result.ShouldContain("FOUR");
        result.ShouldContain("FIVE");
        result.ShouldContain("SIX");
    }

    [Fact]
    public async Task Link_FormatsAsTextWithUrl()
    {
        string result = await Converter.ConvertAsync(
            """<a href="https://example.com">Click here</a>""", CancellationToken);
        result.ShouldBe("Click here (https://example.com)");
    }

    [Fact]
    public async Task MailtoLink_WhenTextMatchesEmail_OmitsUrl()
    {
        string result = await Converter.ConvertAsync(
            """<a href="mailto:support@test.com">support@test.com</a>""", CancellationToken);
        result.ShouldBe("support@test.com");
    }

    [Fact]
    public async Task Link_WhenTextIsUrl_DoesNotDuplicate()
    {
        string result = await Converter.ConvertAsync(
            """<a href="https://example.com">https://example.com</a>""", CancellationToken);
        result.ShouldBe("https://example.com");
    }

    [Fact]
    public async Task Strong_WrapsWithAsterisks()
    {
        string result = await Converter.ConvertAsync(
            "<p>This is <strong>important</strong> text</p>", CancellationToken);
        result.ShouldBe("This is *important* text");
    }

    [Fact]
    public async Task Bold_WrapsWithAsterisks()
    {
        string result = await Converter.ConvertAsync(
            "<p>This is <b>bold</b> text</p>", CancellationToken);
        result.ShouldBe("This is *bold* text");
    }

    [Fact]
    public async Task Emphasis_WrapsWithUnderscores()
    {
        string result = await Converter.ConvertAsync(
            "<p>This is <em>emphasized</em> text</p>", CancellationToken);
        result.ShouldBe("This is _emphasized_ text");
    }

    [Fact]
    public async Task UnorderedList_UsesBullets()
    {
        string result = await Converter.ConvertAsync(
            "<ul><li>Apple</li><li>Banana</li><li>Cherry</li></ul>", CancellationToken);
        result.ShouldContain("• Apple");
        result.ShouldContain("• Banana");
        result.ShouldContain("• Cherry");
    }

    [Fact]
    public async Task OrderedList_UsesNumbers()
    {
        string result = await Converter.ConvertAsync(
            "<ol><li>First</li><li>Second</li><li>Third</li></ol>", CancellationToken);
        result.ShouldContain("1. First");
        result.ShouldContain("2. Second");
        result.ShouldContain("3. Third");
    }

    [Fact]
    public async Task HorizontalRule_ProducesSeparator()
    {
        string result = await Converter.ConvertAsync(
            "<p>Above</p><hr><p>Below</p>", CancellationToken);
        result.ShouldContain("---");
    }

    [Fact]
    public async Task Image_ExtractsAltText()
    {
        string result = await Converter.ConvertAsync(
            """<img src="logo.png" alt="Company Logo">""", CancellationToken);
        result.ShouldBe("[Company Logo]");
    }

    [Fact]
    public async Task Image_NoAlt_ProducesNothing()
    {
        string result = await Converter.ConvertAsync(
            """<img src="spacer.gif">""", CancellationToken);
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task StyleTag_IsStripped()
    {
        string result = await Converter.ConvertAsync(
            "<style>body { color: red; }</style><p>Visible text</p>", CancellationToken);
        result.ShouldBe("Visible text");
        result.ShouldNotContain("color");
    }

    [Fact]
    public async Task ScriptTag_IsStripped()
    {
        string result = await Converter.ConvertAsync(
            "<script>alert('xss')</script><p>Safe text</p>", CancellationToken);
        result.ShouldBe("Safe text");
        result.ShouldNotContain("alert");
    }

    [Fact]
    public async Task LayoutTable_WithRolePresentation_TraversesChildren()
    {
        string result = await Converter.ConvertAsync(
            """<table role="presentation"><tr><td>Cell content</td></tr></table>""", CancellationToken);
        result.ShouldContain("Cell content");
    }

    [Fact]
    public async Task DividerTable_ProducesSeparator()
    {
        string result = await Converter.ConvertAsync("""
            <table>
                <tr>
                    <td style="border-top: 1px solid #d1d5db; font-size: 0; line-height: 0;" height="1">&nbsp;</td>
                </tr>
            </table>
            """, CancellationToken);
        result.ShouldContain("---");
    }

    [Fact]
    public async Task HtmlEntities_AreDecoded()
    {
        string result = await Converter.ConvertAsync(
            "<p>&copy; 2026 Granit &amp; Co</p>", CancellationToken);
        result.ShouldContain("© 2026 Granit & Co");
    }

    [Fact]
    public async Task ExcessiveWhitespace_IsCollapsed()
    {
        string result = await Converter.ConvertAsync(
            "<p>Hello     world</p>", CancellationToken);
        result.ShouldBe("Hello world");
    }

    [Fact]
    public async Task MoreThanTwoNewlines_CollapsedToTwo()
    {
        string result = await Converter.ConvertAsync(
            "<p>Above</p><p></p><p></p><p></p><p>Below</p>", CancellationToken);
        result.ShouldNotContain("\n\n\n");
    }

    [Fact]
    public async Task MsoConditionalComments_AreStripped()
    {
        string result = await Converter.ConvertAsync("""
            <!--[if mso]><table width="600"><tr><td><![endif]-->
            <p>Content</p>
            <!--[if mso]></td></tr></table><![endif]-->
            """, CancellationToken);
        result.ShouldBe("Content");
    }

    [Fact]
    public async Task FullEmailLayout_ProducesCleanText()
    {
        const string html = """
            <html>
            <head><title>Test Email</title></head>
            <body style="margin: 0; padding: 0; background-color: #f4f5f7;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%">
                    <tr>
                        <td align="center" style="padding: 32px 0;">
                            <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="max-width: 600px;">
                                <tr>
                                    <td align="center" style="background-color: #1a1a2e; padding: 24px 32px;">
                                        <span style="color: #ffffff; font-size: 20px;">MyApp</span>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding: 32px;">
                                        <p>Hello <strong>John</strong>,</p>
                                        <p>Your account has been created.</p>
                                        <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%">
                                            <tr>
                                                <td style="border-top: 1px solid #d1d5db;" height="1">&nbsp;</td>
                                            </tr>
                                        </table>
                                        <table role="presentation"><tr><td align="center">
                                            <a href="https://myapp.com" style="color: #1d4ed8;">MyApp</a>
                                        </td></tr></table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="background-color: #f3f4f6; padding: 20px 32px; text-align: center;">
                                        <p style="font-size: 13px;"><a href="https://myapp.com/unsub" style="color: #374151;">Manage preferences</a></p>
                                        <p style="font-size: 13px;">Do not reply to this email</p>
                                        <p style="font-size: 13px;">&copy; 2026 MyApp</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;

        string result = await Converter.ConvertAsync(html, CancellationToken);

        result.ShouldContain("MyApp");
        result.ShouldContain("Hello *John*,");
        result.ShouldContain("Your account has been created.");
        result.ShouldContain("---");
        result.ShouldContain("Manage preferences (https://myapp.com/unsub)");
        result.ShouldContain("Do not reply to this email");
        result.ShouldContain("© 2026 MyApp");
        result.ShouldNotContain("<");
        result.ShouldNotContain(">");
    }

    [Fact]
    public async Task CancellationToken_IsRespected()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<TaskCanceledException>(
            () => Converter.ConvertAsync("<p>test</p>", cts.Token));
    }
}
