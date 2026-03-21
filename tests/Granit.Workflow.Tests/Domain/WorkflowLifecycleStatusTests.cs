using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests.Domain;

/// <summary>
/// Tests for <see cref="WorkflowLifecycleStatus"/> enum values and ordering.
/// These values are persisted in databases — changing them is a breaking change.
/// </summary>
public sealed class WorkflowLifecycleStatusTests
{
    [Fact]
    public void Draft_ShouldHaveValue0() =>
        ((int)WorkflowLifecycleStatus.Draft).ShouldBe(0);

    [Fact]
    public void PendingReview_ShouldHaveValue1() =>
        ((int)WorkflowLifecycleStatus.PendingReview).ShouldBe(1);

    [Fact]
    public void Published_ShouldHaveValue2() =>
        ((int)WorkflowLifecycleStatus.Published).ShouldBe(2);

    [Fact]
    public void Archived_ShouldHaveValue3() =>
        ((int)WorkflowLifecycleStatus.Archived).ShouldBe(3);

    [Fact]
    public void Enum_ShouldHaveExactlyFourValues()
    {
        // Assert — guard against accidental additions that break stored values
        string[] names = Enum.GetNames<WorkflowLifecycleStatus>();
        names.Length.ShouldBe(4);
    }
}
