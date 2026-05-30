using Granit.AI;
using Granit.Workflow.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmRecommendationResponse = Granit.Workflow.AI.Internal.LlmTransitionAdvisor.LlmRecommendationResponse;

namespace Granit.Workflow.AI.Tests;

public sealed class LlmTransitionAdvisorTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<WorkflowAIOptions> _options = Microsoft.Extensions.Options.Options.Create(
        new WorkflowAIOptions { WorkspaceName = "test", TimeoutSeconds = 10 });

    private LlmTransitionAdvisor CreateSut() => new(_structured, _options, NullLogger<LlmTransitionAdvisor>.Instance);

    private void Succeeds(LlmRecommendationResponse value) =>
        _structured
            .CompleteAsync<LlmRecommendationResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRecommendationResponse> { Status = StructuredCompletionStatus.Succeeded, Value = value });

    private void Fails(StructuredCompletionStatus status) =>
        _structured
            .CompleteAsync<LlmRecommendationResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRecommendationResponse> { Status = status });

    [Fact]
    public async Task RecommendAsync_ValidResponse_ReturnsRecommendation()
    {
        Succeeds(new LlmRecommendationResponse("Publish", "Document is complete.", 0.92));

        TransitionRecommendation? result = await CreateSut().RecommendAsync(
            "Document", "Draft", """{"title":"Report"}""", ["Publish", "Archive"], TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.RecommendedTransition.ShouldBe("Publish");
        result.Reasoning.ShouldBe("Document is complete.");
        result.Confidence.ShouldBe(0.92);
    }

    [Fact]
    public async Task RecommendAsync_EmptyAllowedTransitions_ReturnsNull()
    {
        TransitionRecommendation? result = await CreateSut().RecommendAsync(
            "Document", "Archived", "{}", [], TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_SchemaViolation_ReturnsNull()
    {
        Fails(StructuredCompletionStatus.SchemaViolation);

        TransitionRecommendation? result = await CreateSut().RecommendAsync(
            "Document", "Draft", "{}", ["Publish"], TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_TransportFailure_ReturnsNull()
    {
        Fails(StructuredCompletionStatus.TransportFailure);

        TransitionRecommendation? result = await CreateSut().RecommendAsync(
            "Invoice", "Pending", "{}", ["Approve", "Reject"], TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_RecommendationNotInAllowedSet_ReturnsNull()
    {
        Succeeds(new LlmRecommendationResponse("Delete", "Hallucinated.", 0.9));

        TransitionRecommendation? result = await CreateSut().RecommendAsync(
            "Document", "Draft", "{}", ["Publish", "Archive"], TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RecommendAsync_ConfidenceAboveOne_ClampedToOne()
    {
        Succeeds(new LlmRecommendationResponse("Approve", "Looks good.", 1.5));

        TransitionRecommendation? result = await CreateSut().RecommendAsync(
            "Invoice", "Pending", "{}", ["Approve"], TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Confidence.ShouldBe(1.0);
    }

    [Fact]
    public async Task RecommendAsync_PassesContextAndAllowedTransitions()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<LlmRecommendationResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmRecommendationResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new LlmRecommendationResponse("Publish", "ok", 0.8),
            });

        await CreateSut().RecommendAsync("Document", "Draft", "the context", ["Publish", "Archive"], TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.ShouldBe("the context");
        captured.Instruction!.ShouldContain("Publish, Archive"); // allowed transitions injected
        captured.Context.ShouldNotBeNull();
        captured.Context.ShouldContain(kv => kv.Key == "Entity type" && kv.Value == "Document");
        captured.Context.ShouldContain(kv => kv.Key == "Current state" && kv.Value == "Draft");
    }

    [Fact]
    public async Task RecommendAsync_NullEntityType_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            CreateSut().RecommendAsync(null!, "Draft", "{}", ["Publish"], TestContext.Current.CancellationToken));
}
