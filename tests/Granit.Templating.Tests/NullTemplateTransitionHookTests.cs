using Granit.Templating.Store;
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
    [InlineData(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, true)]
    [InlineData(TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived, true)]
    [InlineData(TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Draft, true)]
    [InlineData(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Archived, false)]
    [InlineData(TemplateLifecycleStatus.Archived, TemplateLifecycleStatus.Draft, false)]
    [InlineData(TemplateLifecycleStatus.Archived, TemplateLifecycleStatus.Published, false)]
    [InlineData(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.PendingReview, false)]
    [InlineData(TemplateLifecycleStatus.PendingReview, TemplateLifecycleStatus.Published, false)]
    public async Task CanTransitionAsync_ReturnsExpectedResult(
        TemplateLifecycleStatus from, TemplateLifecycleStatus to, bool expected)
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
            TemplateLifecycleStatus.Draft,
            TemplateLifecycleStatus.Published,
            "alice",
            TestContext.Current.CancellationToken));
    }
}
