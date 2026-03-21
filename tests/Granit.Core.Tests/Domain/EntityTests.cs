using Granit.Core.Domain;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Domain;

public sealed class EntityTests
{
    // -------------------------------------------------------------------------
    // Id property
    // -------------------------------------------------------------------------

    [Fact]
    public void Id_DefaultsToEmptyGuid()
    {
        TestEntity entity = new();

        entity.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Id_CanBeAssigned()
    {
        var id = Guid.NewGuid();
        TestEntity entity = new() { Id = id };

        entity.Id.ShouldBe(id);
    }

    // -------------------------------------------------------------------------
    // CreationAuditedEntity
    // -------------------------------------------------------------------------

    [Fact]
    public void CreationAuditedEntity_DefaultValues_AreCorrect()
    {
        TestCreationAuditedEntity entity = new();

        entity.Id.ShouldBe(Guid.Empty);
        entity.CreatedAt.ShouldBe(default);
        entity.CreatedBy.ShouldBeEmpty();
    }

    [Fact]
    public void CreationAuditedEntity_InheritsEntity()
    {
        TestCreationAuditedEntity entity = new();

        entity.ShouldBeAssignableTo<Entity>();
    }

    [Fact]
    public void CreationAuditedEntity_Properties_CanBeAssigned()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TestCreationAuditedEntity entity = new()
        {
            CreatedAt = now,
            CreatedBy = "user-1",
        };

        entity.CreatedAt.ShouldBe(now);
        entity.CreatedBy.ShouldBe("user-1");
    }

    // -------------------------------------------------------------------------
    // AuditedEntity
    // -------------------------------------------------------------------------

    [Fact]
    public void AuditedEntity_DefaultModificationValues_AreNull()
    {
        TestAuditedEntity entity = new();

        entity.ModifiedAt.ShouldBeNull();
        entity.ModifiedBy.ShouldBeNull();
    }

    [Fact]
    public void AuditedEntity_InheritsCreationAuditedEntity()
    {
        TestAuditedEntity entity = new();

        entity.ShouldBeAssignableTo<CreationAuditedEntity>();
        entity.ShouldBeAssignableTo<Entity>();
    }

    [Fact]
    public void AuditedEntity_Properties_CanBeAssigned()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TestAuditedEntity entity = new()
        {
            CreatedAt = now,
            CreatedBy = "creator",
            ModifiedAt = now.AddHours(1),
            ModifiedBy = "modifier",
        };

        entity.CreatedAt.ShouldBe(now);
        entity.CreatedBy.ShouldBe("creator");
        entity.ModifiedAt.ShouldBe(now.AddHours(1));
        entity.ModifiedBy.ShouldBe("modifier");
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private sealed class TestEntity : Entity;

    private sealed class TestCreationAuditedEntity : CreationAuditedEntity;

    private sealed class TestAuditedEntity : AuditedEntity;
}
