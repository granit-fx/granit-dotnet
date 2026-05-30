using Granit.AI;
using Granit.Validation.AI.Internal;
using Granit.Validation.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmModerationFlag = Granit.Validation.AI.Internal.LlmContentModerator.LlmModerationFlag;
using LlmModerationResponse = Granit.Validation.AI.Internal.LlmContentModerator.LlmModerationResponse;

namespace Granit.Validation.AI.Tests;

public sealed class LlmContentModeratorAdditionalTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<ValidationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new ValidationAIOptions { WorkspaceName = "default", TimeoutSeconds = 2, SeverityThreshold = 0.5 });

    private LlmContentModerator CreateModerator() =>
        new(_structured, _options, NullLogger<LlmContentModerator>.Instance);

    private void Succeeds(LlmModerationResponse value) =>
        _structured
            .CompleteAsync<LlmModerationResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmModerationResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = value,
            });

    private async Task<ModerationResult> ModerateAsync(LlmModerationResponse value)
    {
        Succeeds(value);
        return await CreateModerator().ModerateAsync("some text", cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ModerateAsync_NullText_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateModerator().ModerateAsync(null!, cancellationToken: TestContext.Current.CancellationToken));

    [Fact]
    public async Task ModerateAsync_NullFlags_ReturnsAcceptableNoFlags()
    {
        ModerationResult result = await ModerateAsync(new LlmModerationResponse(true, null));

        result.IsAcceptable.ShouldBeTrue();
        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ModerateAsync_UnknownCategory_IsFiltered()
    {
        // "Unknown" is not a ModerationCategory enum value — TryParse fails, flag is skipped.
        ModerationResult result = await ModerateAsync(
            new LlmModerationResponse(false, [new LlmModerationFlag("Unknown", "test", 0.9)]));

        result.Flags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ModerateAsync_FlagWithNullDescription_UsesEmptyString()
    {
        ModerationResult result = await ModerateAsync(
            new LlmModerationResponse(false, [new LlmModerationFlag("Toxic", null, 0.9)]));

        result.Flags.Count.ShouldBe(1);
        result.Flags[0].Description.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ModerateAsync_AllCategoriesAboveThreshold_ReturnsAll()
    {
        ModerationResult result = await ModerateAsync(new LlmModerationResponse(false,
        [
            new LlmModerationFlag("Violence", "Violent content", 0.7),
            new LlmModerationFlag("SelfHarm", "Self-harm content", 0.6),
            new LlmModerationFlag("Sexual", "Sexual content", 0.8),
        ]));

        result.Flags.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ModerateAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        _structured
            .CompleteAsync<LlmModerationResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<StructuredCompletionResult<LlmModerationResponse>>(callInfo =>
            {
                CancellationToken token = callInfo.ArgAt<CancellationToken>(1);
                token.ThrowIfCancellationRequested();
                throw new OperationCanceledException(token);
            });

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Caller cancellation (not the moderation timeout) must propagate, not fail-open.
        await Should.ThrowAsync<OperationCanceledException>(
            () => CreateModerator().ModerateAsync("test", cancellationToken: cts.Token));
    }
}
