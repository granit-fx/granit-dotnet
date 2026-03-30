// =============================================================================
// Tests — DataSeedContext
// =============================================================================
// Vérifie que le contexte de seeding :
//   - Stocke correctement le TenantId (null ou valeur)
//   - Gère le dictionnaire Properties via l'indexeur
//   - Retourne null pour une clé inexistante
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.DataSeeding;

public sealed class DataSeedContextTests
{
    [Fact]
    public void Constructor_WithNoTenantId_TenantIdIsNull()
    {
        // Act
        DataSeedContext context = new();

        // Assert
        context.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithTenantId_StoresTenantId()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        DataSeedContext context = new(tenantId);

        // Assert
        context.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Properties_IsEmptyByDefault()
    {
        // Act
        DataSeedContext context = new();

        // Assert
        context.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void Properties_SetAndGet_ReturnsValue()
    {
        // Arrange
        DataSeedContext context = new();

        // Act
        context.Properties["AdminEmail"] = "admin@example.com";

        // Assert
        context.Properties["AdminEmail"].ShouldBe("admin@example.com");
    }

    [Fact]
    public void Indexer_SetAndGet_ReturnsValue()
    {
        // Arrange
        DataSeedContext context = new();

        // Act
        context["Key"] = 42;

        // Assert
        context["Key"].ShouldBe(42);
    }

    [Fact]
    public void Indexer_UnknownKey_ReturnsNull()
    {
        // Arrange
        DataSeedContext context = new();

        // Act
        object? value = context["NonExistent"];

        // Assert
        value.ShouldBeNull();
    }
}
