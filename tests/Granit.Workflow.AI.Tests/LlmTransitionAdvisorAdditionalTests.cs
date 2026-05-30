using Granit.AI;
using Granit.Workflow.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmRecommendationResponse = Granit.Workflow.AI.Internal.LlmTransitionAdvisor.LlmRecommendationResponse;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Additional tests for <see cref="LlmTransitionAdvisor"/> covering null parameter checks
/// and edge cases not covered by the main test class.
/// </summary>
public sealed class LlmTransitionAdvisorAdditionalTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<WorkflowAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new WorkflowAIOptions { WorkspaceName = "test", TimeoutSeconds = 10 });

    private LlmTransitionAdvisor CreateSut() => new(_structured, _options, NullLogger<LlmTransitionAdvisor>.Instance);

    private async Task<TransitionRecommendation?> Recommend(LlmRecommendationResponse value)
    {
        _structured
            .CompleteAsync<LlmRecommendationResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRecommendationResponse> { Status = StructuredCompletionStatus.Succeeded, Value = value });
        return await CreateSut().RecommendAsync("Document", "Draft", "{}", ["Publish"], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RecommendAsync_NullCurrentState_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            CreateSut().RecommendAsync("Document", null!, "{}", ["Publish"], TestContext.Current.CancellationToken));

    [Fact]
    public async Task RecommendAsync_NullEntityContext_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            CreateSut().RecommendAsync("Document", "Draft", null!, ["Publish"], TestContext.Current.CancellationToken));

    [Fact]
    public async Task RecommendAsync_NullAllowedTransitions_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            CreateSut().RecommendAsync("Document", "Draft", "{}", null!, TestContext.Current.CancellationToken));

    [Fact]
    public async Task RecommendAsync_ConfidenceBelowZero_ClampedToZero()
    {
        TransitionRecommendation? result = await Recommend(new LlmRecommendationResponse("Publish", "Negative confidence.", -0.5));

        result.ShouldNotBeNull();
        result.Confidence.ShouldBe(0.0);
    }

    [Fact]
    public async Task RecommendAsync_NullRecommendedTransition_ReturnsNull()
    {
        TransitionRecommendation? result = await Recommend(new LlmRecommendationResponse(null, "Cannot decide.", 0.1));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_EmptyRecommendedTransition_ReturnsNull()
    {
        TransitionRecommendation? result = await Recommend(new LlmRecommendationResponse("", "Cannot decide.", 0.1));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_NullReasoningInResponse_ReturnsEmptyString()
    {
        TransitionRecommendation? result = await Recommend(new LlmRecommendationResponse("Publish", null, 0.8));

        result.ShouldNotBeNull();
        result.Reasoning.ShouldBe(string.Empty);
    }
}
