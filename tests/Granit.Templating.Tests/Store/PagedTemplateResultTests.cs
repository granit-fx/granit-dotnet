using Granit.Templating.Store;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Store;

public sealed class PagedTemplateResultTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        List<TemplateSummary> items =
        [
            new TemplateSummary
            {
                Name = "Billing.Invoice",
                Culture = null,
                MimeType = "text/html",
                CurrentStatus = TemplateLifecycleStatus.Published,
                LastModifiedAt = DateTimeOffset.UtcNow,
                LastModifiedBy = "alice",
                HasPublishedVersion = true,
            },
        ];

        PagedTemplateResult result = new(items, 10);

        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(10);
    }

    [Fact]
    public void EmptyResult_HasZeroItems()
    {
        PagedTemplateResult result = new([], 0);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public void Equality_SameData_AreEqual()
    {
        IReadOnlyList<TemplateSummary> items = [];
        PagedTemplateResult result1 = new(items, 5);
        PagedTemplateResult result2 = new(items, 5);

        result1.ShouldBe(result2);
    }
}
