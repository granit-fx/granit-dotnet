using System.Text.Json;
using Granit.AI.Tools;
using Granit.Imaging.AI.Internal;
using NSubstitute;
using Shouldly;

namespace Granit.Imaging.AI.Tests;

public sealed class ExtractTextFromImageToolTests
{
    private static readonly ReadOnlyMemory<byte> Image = new byte[] { 1, 2, 3 };

    private static AIToolInvocationContext Args(object value) =>
        new() { Arguments = JsonSerializer.SerializeToElement(value) };

    [Fact]
    public void Has_the_expected_name_and_required_argument()
    {
        ExtractTextFromImageTool tool = new(
            Substitute.For<IAIImageSource>(), Substitute.For<IImageTextExtractor>());

        tool.Name.ShouldBe("extract_text_from_image");
        tool.ParameterSchema.GetProperty("required")[0].GetString().ShouldBe("image");
    }

    [Fact]
    public async Task Missing_image_reference_returns_an_error()
    {
        ExtractTextFromImageTool tool = new(
            Substitute.For<IAIImageSource>(), Substitute.For<IImageTextExtractor>());

        AIToolResult result = await tool.InvokeAsync(Args(new { }), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task Unresolvable_image_returns_an_error()
    {
        IAIImageSource source = Substitute.For<IAIImageSource>();
        source.GetImageAsync("img-1", Arg.Any<CancellationToken>()).Returns((AIImageData?)null);
        ExtractTextFromImageTool tool = new(source, Substitute.For<IImageTextExtractor>());

        AIToolResult result = await tool.InvokeAsync(
            Args(new { image = "img-1" }), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.Content.ShouldContain("img-1");
    }

    [Fact]
    public async Task Degrades_gracefully_when_no_vision_workspace_is_available()
    {
        IAIImageSource source = Substitute.For<IAIImageSource>();
        source.GetImageAsync("img-1", Arg.Any<CancellationToken>())
            .Returns(new AIImageData(Image, "image/png"));
        IImageTextExtractor extractor = Substitute.For<IImageTextExtractor>();
        extractor.ExtractTextAsync(Arg.Any<ReadOnlyMemory<byte>>(), "image/png", Arg.Any<CancellationToken>())
            .Returns((ImageTextExtractionResult?)null);
        ExtractTextFromImageTool tool = new(source, extractor);

        AIToolResult result = await tool.InvokeAsync(
            Args(new { image = "img-1" }), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.Content.ShouldContain("vision");
    }

    [Fact]
    public async Task Returns_the_extracted_text_on_success()
    {
        IAIImageSource source = Substitute.For<IAIImageSource>();
        source.GetImageAsync("img-1", Arg.Any<CancellationToken>())
            .Returns(new AIImageData(Image, "image/png"));
        IImageTextExtractor extractor = Substitute.For<IImageTextExtractor>();
        extractor.ExtractTextAsync(Arg.Any<ReadOnlyMemory<byte>>(), "image/png", Arg.Any<CancellationToken>())
            .Returns(new ImageTextExtractionResult("HELLO", "vision"));
        ExtractTextFromImageTool tool = new(source, extractor);

        AIToolResult result = await tool.InvokeAsync(
            Args(new { image = "img-1" }), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        using var payload = JsonDocument.Parse(result.Content);
        payload.RootElement.GetProperty("text").GetString().ShouldBe("HELLO");
        payload.RootElement.GetProperty("workspace").GetString().ShouldBe("vision");
    }
}
