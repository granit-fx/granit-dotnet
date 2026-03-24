// =============================================================================
// Tests - AuditableEntity
// =============================================================================
// Vérifie les valeurs par défaut et l'assignation des propriétés de l'entité
// auditable de base (trail ISO 27001 : créé/modifié).
// =============================================================================

using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tests;

public sealed class AuditableEntityTests
{
    [Fact]
    public void AuditableEntity_DefaultValues_AreCorrect()
    {
        // Act
        ConcreteAuditableEntity entity = new();

        // Assert
        entity.Id.ShouldBe(Guid.Empty);
        entity.CreatedAt.ShouldBe(default);
        entity.CreatedBy.ShouldBeEmpty("default is string.Empty");
        entity.ModifiedAt.ShouldBeNull();
        entity.ModifiedBy.ShouldBeNull();
    }

    [Fact]
    public void AuditableEntity_Properties_CanBeAssigned()
    {
        // Arrange
        var id = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset modified = now.AddHours(1);

        // Act
        ConcreteAuditableEntity entity = new()
        {
            Id = id,
            CreatedAt = now,
            CreatedBy = "user-123",
            ModifiedAt = modified,
            ModifiedBy = "user-456",
        };

        // Assert
        entity.Id.ShouldBe(id);
        entity.CreatedAt.ShouldBe(now);
        entity.CreatedBy.ShouldBe("user-123");
        entity.ModifiedAt.ShouldBe(modified);
        entity.ModifiedBy.ShouldBe("user-456");
    }

    /// <summary>Concrete subclass required to instantiate the abstract base.</summary>
    private sealed class ConcreteAuditableEntity : AuditableEntity;
}
