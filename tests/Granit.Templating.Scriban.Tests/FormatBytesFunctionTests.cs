using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Scriban;
using Granit.Templating.Scriban.Internal;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests;

internal sealed record BytesModel(long Bytes);

public sealed class FormatBytesFunctionTests
{
    private static readonly ScribanTemplateEngine Sut = new(new ServiceCollection().BuildServiceProvider());

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    [InlineData(5L * 1024L * 1024L * 1024L, "5 GB")]
    [InlineData(1099511627776L, "1 TB")]
    [InlineData(-2048L, "-2 KB")]
    public async Task FormatBytes_should_pick_largest_readable_unit(long input, string expected)
    {
        var descriptor = new TemplateDescriptor
        {
            Content = "{{ model.bytes | format_bytes }}",
            MimeType = "text/html",
        };

        RenderedContent result = await Sut.RenderAsync(
            descriptor, new BytesModel(input), DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TextRenderedContent>()
            .Html.ShouldBe(expected);
    }

    [Fact]
    public async Task FormatBytes_should_emit_empty_string_for_null_value()
    {
        var descriptor = new TemplateDescriptor
        {
            Content = "[{{ model.bytes | format_bytes }}]",
            MimeType = "text/html",
        };

        RenderedContent result = await Sut.RenderAsync(
            descriptor,
            new Dictionary<string, object?> { ["bytes"] = null },
            DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TextRenderedContent>()
            .Html.ShouldBe("[]");
    }
}
