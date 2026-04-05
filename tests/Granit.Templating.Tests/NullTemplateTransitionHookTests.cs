using Granit.Templating.Store;
using Granit.Workflow.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests;

public sealed class NullTemplateTransitionHookTests
{
    private readonly NullTemplateTransitionHook _hook = new(NullLogger<NullTemplateTransitionHook>.Instance);

    [Fact]
    public void IsWorkflowEnabled_ReturnsFalse() =>
        _hook.IsWorkflowEnabled.ShouldBeFalse();

    [Theory]
    [InlineData(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published, true)]
    [InlineData(WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived, true)]
    [InlineData(WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Draft, true)]
    [InlineData(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Archived, false)]
    [InlineData(WorkflowLifecycleStatus.Archived, WorkflowLifecycleStatus.Draft, false)]
    [InlineData(WorkflowLifecycleStatus.Archived, WorkflowLifecycleStatus.Published, false)]
    [InlineData(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.PendingReview, false)]
    [InlineData(WorkflowLifecycleStatus.PendingReview, WorkflowLifecycleStatus.Published, false)]
    public async Task CanTransitionAsync_ReturnsExpectedResult(
        WorkflowLifecycleStatus from, WorkflowLifecycleStatus to, bool expected)
    {
        bool result = await _hook.CanTransitionAsync(from, to,
            TestContext.Current.CancellationToken);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task OnTransitionedAsync_CompletesImmediately()
    {
        await Should.NotThrowAsync(() => _hook.OnTransitionedAsync(
            Guid.NewGuid(),
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published,
            "alice",
            TestContext.Current.CancellationToken));
    }
}
