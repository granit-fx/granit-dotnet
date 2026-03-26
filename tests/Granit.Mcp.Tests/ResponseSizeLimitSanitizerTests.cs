using Granit.Mcp.Options;
using Granit.Mcp.Sanitization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Mcp.Tests;

public sealed class ResponseSizeLimitSanitizerTests
{
    [Fact]
    public async Task SanitizeAsync_WhenUnderLimit_ShouldReturnOriginalResult()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions { MaxResponseSizeBytes = 51_200 });
        ResponseSizeLimitSanitizer sut = new(options);
        CallToolResult result = new() { Content = [new TextContentBlock { Text = "Hello" }] };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        sanitized.ShouldBe(result);
    }

    [Fact]
    public async Task SanitizeAsync_WhenOverLimit_ShouldTruncate()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions { MaxResponseSizeBytes = 10 });
        ResponseSizeLimitSanitizer sut = new(options);
        CallToolResult result = new()
        {
            Content = [new TextContentBlock { Text = new string('x', 1000) }],
        };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        sanitized.Content.ShouldHaveSingleItem();
        TextContentBlock textBlock = sanitized.Content[0].ShouldBeOfType<TextContentBlock>();
        textBlock.Text.ShouldContain("truncated");
    }

    [Fact]
    public async Task SanitizeAsync_WhenLimitIsZero_ShouldReturnOriginal()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions { MaxResponseSizeBytes = 0 });
        ResponseSizeLimitSanitizer sut = new(options);
        CallToolResult result = new()
        {
            Content = [new TextContentBlock { Text = new string('x', 100_000) }],
        };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        sanitized.ShouldBe(result);
    }
}
