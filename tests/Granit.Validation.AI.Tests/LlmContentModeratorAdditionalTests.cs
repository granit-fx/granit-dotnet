using Granit.AI;
using Granit.Validation.AI.Internal;
using Granit.Validation.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.Validation.AI.Tests;

public sealed class LlmContentModeratorAdditionalTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IOptions<ValidationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new ValidationAIOptions { WorkspaceName = "default", TimeoutSeconds = 2, SeverityThreshold = 0.5 });

    private LlmContentModerator CreateModerator() =>
        new(_chatClientFactory, _options, NullLogger<LlmContentModerator>.Instance);

    // =========================================================================
    // ModerateAsync — null text
    // =========================================================================

    [Fact]
    public async Task ModerateAsync_NullText_ThrowsArgumentNullException()
    {
        LlmContentModerator moderator = CreateModerator();

        await Should.ThrowAsync<ArgumentNullException>(
            () => moderator.ModerateAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // ParseResponse — edge cases
    // =========================================================================

    [Fact]
    public void ParseResponse_InvalidJson_ThrowsJsonException()
    {
        // ParseResponse does not catch JsonException — the caller (ModerateAsync) handles it
        Should.Throw<System.Text.Json.JsonException>(
            () => LlmContentModerator.ParseResponse("not valid json", 0.5));
    }

    [Fact]
    public void ParseResponse_NullJson_ReturnsAcceptable()
    {
        ModerationResult result = LlmContentModerator.ParseResponse("null", 0.5);

        result.IsAcceptable.ShouldBeTrue();
        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public void ParseResponse_NullFlags_ReturnsAcceptableNoFlags()
    {
        ModerationResult result = LlmContentModerator.ParseResponse(
            """{ "isAcceptable": true, "flags": null }""", 0.5);

        result.IsAcceptable.ShouldBeTrue();
        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public void ParseResponse_UnknownCategory_IsFiltered()
    {
        ModerationResult result = LlmContentModerator.ParseResponse(
            """{ "isAcceptable": false, "flags": [{ "category": "Unknown", "description": "test", "severity": 0.9 }] }""",
            0.5);

        // "Unknown" is not a valid ModerationCategory enum value, so TryParse fails, flag is skipped
        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public void ParseResponse_FlagWithNullDescription_UsesEmptyString()
    {
        ModerationResult result = LlmContentModerator.ParseResponse(
            """{ "isAcceptable": false, "flags": [{ "category": "Toxic", "description": null, "severity": 0.9 }] }""",
            0.5);

        result.Flags.Count.ShouldBe(1);
        result.Flags[0].Description.ShouldBe(string.Empty);
    }

    [Fact]
    public void ParseResponse_MultipleCodeFences_ParsesCorrectly()
    {
        const string response = """
            ```json
            { "isAcceptable": false, "flags": [{ "category": "Harassment", "description": "Bullying", "severity": 0.85 }] }
            ```
            """;

        ModerationResult result = LlmContentModerator.ParseResponse(response, 0.5);

        result.IsAcceptable.ShouldBeFalse();
        result.Flags.Count.ShouldBe(1);
        result.Flags[0].Category.ShouldBe(ModerationCategory.Harassment);
    }

    [Fact]
    public void ParseResponse_AllCategoriesAboveThreshold_ReturnsAll()
    {
        ModerationResult result = LlmContentModerator.ParseResponse(
            """
            {
                "isAcceptable": false,
                "flags": [
                    { "category": "Violence", "description": "Violent content", "severity": 0.7 },
                    { "category": "SelfHarm", "description": "Self-harm content", "severity": 0.6 },
                    { "category": "Sexual", "description": "Sexual content", "severity": 0.8 }
                ]
            }
            """,
            0.5);

        result.Flags.Count.ShouldBe(3);
    }

    // =========================================================================
    // ModerateAsync — cancellation
    // =========================================================================

    [Fact]
    public async Task ModerateAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        _chatClientFactory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<IChatClient>(callInfo =>
            {
                CancellationToken token = callInfo.ArgAt<CancellationToken>(1);
                token.ThrowIfCancellationRequested();
                throw new OperationCanceledException(token);
            });

        LlmContentModerator moderator = CreateModerator();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => moderator.ModerateAsync("test", cancellationToken: cts.Token));
    }
}
