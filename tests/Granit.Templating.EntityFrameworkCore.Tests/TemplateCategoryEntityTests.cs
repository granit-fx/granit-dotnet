using Granit.Templating.EntityFrameworkCore.Entities;
using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

public sealed class TemplateCategoryEntityTests
{
    [Fact]
    public void Properties_can_be_set_and_read()
    {
        var id = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        var entity = new TemplateCategoryEntity
        {
            Id = id,
            Name = "Patient letters",
            Description = "Templates for patient correspondence",
            Icon = "file-text",
            SortOrder = 3,
            CreatedAt = createdAt,
            CreatedBy = "alice",
        };

        entity.Id.ShouldBe(id);
        entity.Name.ShouldBe("Patient letters");
        entity.Description.ShouldBe("Templates for patient correspondence");
        entity.Icon.ShouldBe("file-text");
        entity.SortOrder.ShouldBe(3);
        entity.CreatedAt.ShouldBe(createdAt);
        entity.CreatedBy.ShouldBe("alice");
    }

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
