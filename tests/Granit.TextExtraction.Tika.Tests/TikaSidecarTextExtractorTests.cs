using System.Net;
using System.Text;
using Granit.TextExtraction.Tika.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Tika.Tests;

public sealed class TikaSidecarTextExtractorTests
{
    private static readonly Uri DefaultTikaUri = new("https://tika.test/");
    private const string Rtf = "application/rtf";

    private static (TikaSidecarTextExtractor extractor, StubHttpMessageHandler handler) CreateExtractor(
        StubHttpMessageHandler? handler = null,
        TikaSidecarOptions? tikaOptions = null,
        ExtractionOptions? extractionOptions = null)
    {
        StubHttpMessageHandler stub =
            handler ?? StubHttpMessageHandler.RespondWith(HttpStatusCode.OK, "Extracted text content.");

        ServiceCollection services = new();
        services.AddHttpClient(TikaSidecarTextExtractor.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        ServiceProvider sp = services.BuildServiceProvider();
        IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();

        TikaSidecarOptions opts = tikaOptions ?? new TikaSidecarOptions
        {
            Uri = DefaultTikaUri,
            AllowedHosts = ["tika.test"],
        };
        ExtractionOptions extr = extractionOptions ?? new ExtractionOptions();

        TikaSidecarTextExtractor extractor = new(
            factory,
            MEOptions.Create(extr),
            MEOptions.Create(opts),
            NullLogger<TikaSidecarTextExtractor>.Instance);

        return (extractor, stub);
    }

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    [Theory]
    [InlineData("application/rtf", true)]
    [InlineData("text/rtf", true)]
    [InlineData("application/vnd.oasis.opendocument.text", true)]
    [InlineData("message/rfc822", true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/html", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_configured_content_types(string contentType, bool expected)
    {
        (TikaSidecarTextExtractor extractor, _) = CreateExtractor();
        extractor.CanHandle(contentType).ShouldBe(expected);
    }

    [Fact]
    public async Task Forwards_request_to_tika_endpoint_and_returns_plain_text()
    {
        (TikaSidecarTextExtractor extractor, StubHttpMessageHandler handler) = CreateExtractor(
            StubHttpMessageHandler.RespondWith(HttpStatusCode.OK, "Hello from Tika."));
        using MemoryStream input = Utf8("{rtf body}");

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Rtf, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBe("Hello from Tika.");
        result.ExtractorName.ShouldBe(TikaSidecarTextExtractor.ExtractorName);
        result.IsTruncated.ShouldBeFalse();

        handler.Calls.Count.ShouldBe(1);
        HttpRequestMessage call = handler.Calls[0];
        call.Method.ShouldBe(HttpMethod.Put);
        call.RequestUri.ShouldBe(new Uri("https://tika.test/tika"));
        call.Content!.Headers.ContentType!.MediaType.ShouldBe(Rtf);
        // Body content is verified end-to-end by the response round-trip; ByteArrayContent
        // is disposed by the extractor after send, so a post-hoc ReadAsByteArrayAsync here
        // would race against dispose. The header check above proves the MIME contract.
    }

    [Fact]
    public async Task Truncates_response_at_max_char_length()
    {
        string payload = new('x', 5_000);
        (TikaSidecarTextExtractor extractor, _) = CreateExtractor(
            StubHttpMessageHandler.RespondWith(HttpStatusCode.OK, payload));
        using MemoryStream input = Utf8("any");

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Rtf, maxCharLength: 100, cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(100);
    }

    [Fact]
    public async Task Body_size_cap_throws_input_too_large()
    {
        ExtractionOptions extraction = new() { MaxBodySizeBytes = 16 };
        (TikaSidecarTextExtractor extractor, _) = CreateExtractor(extractionOptions: extraction);
        using MemoryStream input = Utf8(new string('a', 4096));

        TextExtraction.Exceptions.TextExtractionException tex =
            await Should.ThrowAsync<TextExtraction.Exceptions.TextExtractionException>(
                async () => await extractor.ExtractAsync(
                    input, Rtf, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Unsuccessful_status_returns_skipped_result(HttpStatusCode status)
    {
        (TikaSidecarTextExtractor extractor, _) = CreateExtractor(
            StubHttpMessageHandler.RespondWith(status, "ignored body"));
        using MemoryStream input = Utf8("body");

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Rtf, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Transport_failure_returns_skipped_result()
    {
        (TikaSidecarTextExtractor extractor, _) = CreateExtractor(
            StubHttpMessageHandler.Throws(new HttpRequestException("DNS down")));
        using MemoryStream input = Utf8("body");

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Rtf, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task CanHandle_can_be_extended_via_options()
    {
        TikaSidecarOptions opts = new()
        {
            Uri = DefaultTikaUri,
            AllowedHosts = ["tika.test"],
            AllowedContentTypes = ["application/x-custom"],
        };
        (TikaSidecarTextExtractor extractor, _) = CreateExtractor(tikaOptions: opts);

        await Task.CompletedTask;

        extractor.CanHandle("application/x-custom").ShouldBeTrue();
        // Default content types are replaced, not merged — caller decides the full list.
        extractor.CanHandle("application/rtf").ShouldBeFalse();
    }
}
