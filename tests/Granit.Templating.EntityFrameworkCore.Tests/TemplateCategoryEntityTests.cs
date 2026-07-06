using Granit.Templating.EntityFrameworkCore.Entities;
using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

public sealed class TemplateCategoryEntityTests
{

    [Fact]
    public void Optional_properties_default_to_null()
    {
        var entity = new TemplateCategoryEntity
        {
            Id = Guid.NewGuid(),
            Name = "Invoices",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "bob",
        };

        entity.Description.ShouldBeNull();
        entity.Icon.ShouldBeNull();
        entity.SortOrder.ShouldBe(0);
    }
}
