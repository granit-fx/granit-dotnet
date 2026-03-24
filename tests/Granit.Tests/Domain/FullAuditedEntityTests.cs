using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain;

public sealed class FullAuditedEntityTests
{
    private sealed class TestEntity : FullAuditedEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        TestEntity entity = new();

        entity.IsDeleted.ShouldBeFalse();
        entity.DeletedAt.ShouldBeNull();
        entity.DeletedBy.ShouldBeNull();
    }

    [Fact]
    public void SoftDelete_SetsAllProperties()
    {
        DateTimeOffset deletedAt = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        TestEntity entity = new()
        {
            IsDeleted = true,
            DeletedAt = deletedAt,
            DeletedBy = "user-123",
        };

        entity.IsDeleted.ShouldBeTrue();
        entity.DeletedAt.ShouldBe(deletedAt);
        entity.DeletedBy.ShouldBe("user-123");
    }

    [Fact]
    public void ImplementsISoftDeletable() =>
        new TestEntity().ShouldBeAssignableTo<ISoftDeletable>();

    [Fact]
    public void InheritsAuditedEntity() =>
        new TestEntity().ShouldBeAssignableTo<AuditedEntity>();

    [Fact]
    public void AuditProperties_AreAccessible()
    {
        DateTimeOffset fixedDate = new(2026, 2, 20, 14, 0, 0, TimeSpan.Zero);
        TestEntity entity = new()
        {
            CreatedAt = fixedDate,
            CreatedBy = "admin",
            ModifiedAt = fixedDate,
            ModifiedBy = "admin",
        };

        entity.CreatedAt.ShouldBe(fixedDate);
        entity.CreatedBy.ShouldBe("admin");
        entity.ModifiedAt.ShouldBe(fixedDate);
        entity.ModifiedBy.ShouldBe("admin");
    }
}
