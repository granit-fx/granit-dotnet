using Granit.Html;
using Granit.Html.AngleSharp;
using Granit.TextExtraction.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Email.Tests;

public sealed class EmailTextExtractorTests
{
    private static readonly IHtmlToPlainTextConverter UntrustedConverter =
        new AngleSharpHtmlToPlainTextConverter(AngleSharpConfiguration.BuildForUntrustedContent());

    private static EmailTextExtractor CreateExtractor(ExtractionOptions? options = null) =>
        new(UntrustedConverter,
            MEOptions.Create(options ?? new ExtractionOptions()),
            NullLogger<EmailTextExtractor>.Instance);

    [Theory]
    [InlineData("message/rfc822", true)]
    [InlineData("MESSAGE/RFC822", true)]
    [InlineData("application/eml", true)]
    [InlineData("Application/EML", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_eml_mime_types(string contentType, bool expected) =>
        CreateExtractor().CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task Extracts_envelope_and_plain_body_from_text_only_message()
    {
        byte[] eml = EmlFixtures.PlainTextOnly(subject: "Quarterly figures");
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Subject: Quarterly figures");
        result.Content.ShouldContain("From: \"Alice Doe\" <alice@example.com>");
        result.Content.ShouldContain("To: \"Bob Smith\" <bob@example.com>");
        result.Content.ShouldContain("Date: Mon, 25 May 2026 10:30:00 GMT");
        result.Content.ShouldContain("The Q3 numbers are ready.");
        result.ExtractorName.ShouldBe(EmailTextExtractor.ExtractorName);
        result.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Prefers_plain_text_part_in_multipart_alternative()
    {
        byte[] eml = EmlFixtures.MultipartAlternative(
            plainBody: "PLAIN-MARKER body",
            htmlBody: "<p>HTML-MARKER body</p>");
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("PLAIN-MARKER body");
        result.Content.ShouldNotContain("HTML-MARKER");
    }

    [Fact]
    public async Task Falls_back_to_html_body_when_no_plain_alternative()
    {
        byte[] eml = EmlFixtures.HtmlOnly("<html><body><h1>Hello</h1><p>HTML-MARKER content</p></body></html>");
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("HTML-MARKER content");
        // Plain-text conversion must strip the HTML markup.
        result.Content.ShouldNotContain("<h1>");
        result.Content.ShouldNotContain("<p>");
    }

    [Fact]
    public async Task Includes_Cc_header_when_present()
    {
        byte[] eml = EmlFixtures.WithCc();
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("To: \"To One\" <to1@example.com>, \"To Two\" <to2@example.com>");
        result.Content.ShouldContain("Cc: \"Cc One\" <cc1@example.com>, \"Cc Two\" <cc2@example.com>");
    }

    [Fact]
    public async Task Omits_Cc_header_when_absent()
    {
        byte[] eml = EmlFixtures.PlainTextOnly();
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldNotContain("Cc:");
    }

    [Fact]
    public async Task Walks_nested_multipart_to_find_the_body()
    {
        byte[] eml = EmlFixtures.NestedMultipart();
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("nested plain body");
    }

    [Fact]
    public async Task Decodes_rfc2047_encoded_headers()
    {
        byte[] eml = EmlFixtures.EncodedHeaders();
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Café — naïve");
        result.Content.ShouldContain("Élise Müller");
    }

    [Fact]
    public async Task Ignores_attachments_but_keeps_envelope_and_body()
    {
        byte[] eml = EmlFixtures.WithAttachment();
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Subject: Report attached");
        result.Content.ShouldContain("See attached.");
        // The attachment payload (or its filename) must NOT leak into the indexed text.
        result.Content.ShouldNotContain("PDF-bytes-go-here");
        result.Content.ShouldNotContain("report.pdf");
    }

    [Fact]
    public async Task Encrypted_multipart_degrades_to_placeholder()
    {
        byte[] eml = EmlFixtures.EncryptedMultipart();
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Subject: Confidential");
        result.Content.ShouldContain("[encrypted message]");
        result.Content.ShouldNotContain("encrypted-blob");
        result.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Malformed_eml_is_soft_skipped()
    {
        byte[] eml = EmlFixtures.Malformed();
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
        result.ExtractorName.ShouldBe(EmailTextExtractor.ExtractorName);
    }

    [Fact]
    public async Task Stops_at_maxCharLength_and_flags_truncation()
    {
        byte[] eml = EmlFixtures.PlainTextOnly(
            subject: "abc",
            body: new string('x', 4000));
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 64, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.Length.ShouldBe(64);
        result.CharCount.ShouldBe(64);
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Body_size_cap_throws_input_too_large()
    {
        ExtractionOptions options = new() { MaxBodySizeBytes = 32 };
        byte[] eml = EmlFixtures.PlainTextOnly(body: "some body that is well above 32 bytes once headers are added");
        using MemoryStream stream = new(eml);

        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => await CreateExtractor(options).ExtractAsync(
                stream, "message/rfc822", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Fact]
    public async Task Sanitises_control_characters_from_subject()
    {
        // Construct a raw message with an embedded LF+colon in the subject — a
        // header-injection attempt that should be neutralised on emit.
        string raw =
            "From: sender@example.com\r\n" +
            "To: rec@example.com\r\n" +
            "Subject: cleansubject\r\n" +
            "Date: Mon, 25 May 2026 10:30:00 +0000\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "body\r\n";
        byte[] eml = System.Text.Encoding.ASCII.GetBytes(raw);
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        //  was stripped; the visible portion of the subject remains contiguous.
        result.Content.ShouldContain("Subject: cleansubject");
        result.Content.ShouldNotContain("");
    }

    [Fact]
    public async Task Strips_embedded_LF_from_subject_to_prevent_log_line_splicing()
    {
        // An RFC 2047 encoded Subject whose decoded value contains a literal LF
        // must NOT survive into the emitted "Subject: …\n" line, or a structured-log
        // consumer would see a spliced fake header.
        //
        // =?utf-8?B?Z29vZApGcm9tOiBhdHRhY2tlckBldmls?= decodes to "good\nFrom: attacker@evil".
        string raw =
            "From: sender@example.com\r\n" +
            "To: rec@example.com\r\n" +
            "Subject: =?utf-8?B?Z29vZApGcm9tOiBhdHRhY2tlckBldmls?=\r\n" +
            "Date: Mon, 25 May 2026 10:30:00 +0000\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "body\r\n";
        byte[] eml = System.Text.Encoding.ASCII.GetBytes(raw);
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        // Decoded payload runs together — no embedded LF survives the sanitiser.
        result.Content.ShouldContain("Subject: goodFrom: attacker@evil");
        // And the splice is not on its own line that a log consumer could mistake for a header.
        int splice = result.Content.IndexOf("From: attacker@evil", StringComparison.Ordinal);
        splice.ShouldBeGreaterThanOrEqualTo(0);
        result.Content[splice - 1].ShouldNotBe('\n');
    }

    [Fact]
    public async Task Strips_carriage_returns_too()
    {
        // Belt-and-braces: \r alone (no \n) is also a structured-log threat.
        string raw =
            "From: sender@example.com\r\n" +
            "To: rec@example.com\r\n" +
            "Subject: =?utf-8?Q?carriage=0Dreturn?=\r\n" +
            "Date: Mon, 25 May 2026 10:30:00 +0000\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "body\r\n";
        byte[] eml = System.Text.Encoding.ASCII.GetBytes(raw);
        using MemoryStream stream = new(eml);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "message/rfc822", maxCharLength: 4096, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Subject: carriagereturn");
        result.Content.ShouldNotContain("\r");
    }

}
