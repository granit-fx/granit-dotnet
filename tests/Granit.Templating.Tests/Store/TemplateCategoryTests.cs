using Granit.Templating.Store;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Store;

public sealed class TemplateCategoryTests
{
    [Fact]
    public void AllRequiredProperties_CanBeSet()
    {
        var id = Guid.NewGuid();

        TemplateCategory category = new()
        {
            Id = id,
            Name = "Patient Letters",
            SortOrder = 1,
            TemplateCount = 5,
        };

        category.Id.ShouldBe(id);
        category.Name.ShouldBe("Patient Letters");
        category.SortOrder.ShouldBe(1);
        category.TemplateCount.ShouldBe(5);
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        TemplateCategory category = new()
        {
            Id = Guid.NewGuid(),
            Name = "Invoices",
            SortOrder = 0,
            TemplateCount = 0,
        };

        category.Description.ShouldBeNull();
        category.Icon.ShouldBeNull();
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        TemplateCategory category = new()
        {
            Id = Guid.NewGuid(),
            Name = "Reports",
            Description = "Quarterly reports",
            Icon = "bar-chart",
            SortOrder = 2,
            TemplateCount = 3,
        };

        category.Description.ShouldBe("Quarterly reports");
        category.Icon.ShouldBe("bar-chart");
    }
}
