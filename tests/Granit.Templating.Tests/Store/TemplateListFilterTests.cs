using Granit.Templating.Store;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Store;

public sealed class TemplateListFilterTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        TemplateListFilter filter = new();

        filter.Page.ShouldBe(1);
        filter.PageSize.ShouldBe(20);
        filter.Search.ShouldBeNull();
        filter.Status.ShouldBeNull();
        filter.Culture.ShouldBeNull();
        filter.CategoryId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsCorrectly()
    {
        var categoryId = Guid.NewGuid();
        TemplateListFilter filter = new(
            Page: 3,
            PageSize: 50,
            Search: "Invoice",
            Status: WorkflowLifecycleStatus.Published,
            Culture: "fr",
            CategoryId: categoryId);

        filter.Page.ShouldBe(3);
        filter.PageSize.ShouldBe(50);
        filter.Search.ShouldBe("Invoice");
        filter.Status.ShouldBe(WorkflowLifecycleStatus.Published);
        filter.Culture.ShouldBe("fr");
        filter.CategoryId.ShouldBe(categoryId);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        TemplateListFilter filter1 = new(Page: 2, PageSize: 10);
        TemplateListFilter filter2 = new(Page: 2, PageSize: 10);

        filter1.ShouldBe(filter2);
    }
}
