using Granit.Mcp.Server.Internal;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Shouldly;

namespace Granit.Mcp.Server.Tests;

public sealed class ErrorSanitizerTests
{
    private readonly ErrorSanitizer _sut = new();

    [Fact]
    public async Task SanitizeAsync_WhenNotError_ShouldReturnOriginal()
    {
        CallToolResult result = new()
        {
            Content = [new TextContentBlock { Text = "Success" }],
            IsError = false,
        };

        CallToolResult sanitized = await _sut.SanitizeAsync(result, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        sanitized.Content[0].ShouldBeOfType<TextContentBlock>()
            .Text.ShouldBe("Success");
    }

    [Fact]
    public async Task SanitizeAsync_WhenErrorWithStackTrace_ShouldStripStackTrace()
    {
        CallToolResult result = new()
        {
            Content = [new TextContentBlock
            {
                Text = "NullReferenceException: Object reference not set\n   at MyApp.MyService.DoWork() in /src/MyService.cs:line 42",
            }],
            IsError = true,
        };

        CallToolResult sanitized = await _sut.SanitizeAsync(result, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        string text = sanitized.Content[0].ShouldBeOfType<TextContentBlock>().Text;
        text.ShouldBe("NullReferenceException: Object reference not set");
        text.ShouldNotContain("at MyApp");
    }

    [Fact]
    public async Task SanitizeAsync_WhenErrorWithConnectionString_ShouldRedact()
    {
        CallToolResult result = new()
        {
            Content = [new TextContentBlock
            {
                Text = "Failed to connect: Server=db.prod.internal;Database=app;Password=secret123",
            }],
            IsError = true,
        };

        CallToolResult sanitized = await _sut.SanitizeAsync(result, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        string text = sanitized.Content[0].ShouldBeOfType<TextContentBlock>().Text;
        text.ShouldBe("An internal error occurred. Check server logs for details.");
    }

    [Fact]
    public async Task SanitizeAsync_WhenErrorWithCleanMessage_ShouldPreserveMessage()
    {
        CallToolResult result = new()
        {
            Content = [new TextContentBlock { Text = "Blob not found: abc-123" }],
            IsError = true,
        };

        CallToolResult sanitized = await _sut.SanitizeAsync(result, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        sanitized.Content[0].ShouldBeOfType<TextContentBlock>()
            .Text.ShouldBe("Blob not found: abc-123");
    }
}
