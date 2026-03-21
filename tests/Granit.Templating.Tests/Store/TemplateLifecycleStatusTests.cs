using Granit.Templating.Store;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Store;

public sealed class TemplateLifecycleStatusTests
{
    [Fact]
    public void Draft_HasValue0() => ((int)TemplateLifecycleStatus.Draft).ShouldBe(0);

    [Fact]
    public void PendingReview_HasValue1() => ((int)TemplateLifecycleStatus.PendingReview).ShouldBe(1);

    [Fact]
    public void Published_HasValue2() => ((int)TemplateLifecycleStatus.Published).ShouldBe(2);

    [Fact]
    public void Archived_HasValue3() => ((int)TemplateLifecycleStatus.Archived).ShouldBe(3);

    [Fact]
    public void AllValues_AreFour()
    {
        TemplateLifecycleStatus[] values = Enum.GetValues<TemplateLifecycleStatus>();

        values.Length.ShouldBe(4);
    }
}
