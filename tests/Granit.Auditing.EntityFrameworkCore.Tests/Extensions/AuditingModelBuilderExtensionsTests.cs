// =============================================================================
// AuditingModelBuilderExtensionsTests - ConfigureAuditingModule
// =============================================================================
// Verifies:
//   - Applies entity configurations for AuditEntry, AuditEntityChange, AuditPropertyChange
//   - Tables use the correct prefix and schema
//   - Returns the ModelBuilder for chaining
// =============================================================================

using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Extensions;

/// <summary>
/// Tests that mutate <see cref="GranitAuditingDbProperties.DbTablePrefix"/> must not
/// run in parallel with other classes that build EF Core models using the same static.
/// </summary>
[Collection("AuditingDbProperties")]
public sealed class AuditingModelBuilderExtensionsTests : IDisposable
{
    private readonly string _originalPrefix;
    private readonly string? _originalSchema;

    public AuditingModelBuilderExtensionsTests()
    {
        _originalPrefix = GranitAuditingDbProperties.DbTablePrefix;
        _originalSchema = GranitAuditingDbProperties.DbSchema;
    }

    public void Dispose()
    {
        GranitAuditingDbProperties.DbTablePrefix = _originalPrefix;
        GranitAuditingDbProperties.DbSchema = _originalSchema;
    }

    // -------------------------------------------------------------------------
    // ConfigureAuditingModule — entity type registration
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureAuditingModule_RegistersAuditEntryEntityType()
    {
        // Arrange & Act
        IModel model = BuildModel();

        // Assert
        model.FindEntityType(typeof(AuditEntry)).ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureAuditingModule_RegistersAuditEntityChangeEntityType()
    {
        // Arrange & Act
        IModel model = BuildModel();

        // Assert
        model.FindEntityType(typeof(AuditEntityChange)).ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureAuditingModule_RegistersAuditPropertyChangeEntityType()
    {
        // Arrange & Act
        IModel model = BuildModel();

        // Assert
        model.FindEntityType(typeof(AuditPropertyChange)).ShouldNotBeNull();
    }

    // -------------------------------------------------------------------------
    // ConfigureAuditingModule — table names
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureAuditingModule_AuditEntry_HasCorrectTableName()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntry));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("audit_log_log_entries");
    }

    [Fact]
    public void ConfigureAuditingModule_AuditEntityChange_HasCorrectTableName()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntityChange));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("audit_log_entity_changes");
    }

    [Fact]
    public void ConfigureAuditingModule_AuditPropertyChange_HasCorrectTableName()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditPropertyChange));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("audit_log_property_changes");
    }

    // -------------------------------------------------------------------------
    // ConfigureAuditingModule — custom prefix
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureAuditingModule_WithCustomPrefix_UsesCustomPrefix()
    {
        // Arrange
        GranitAuditingDbProperties.DbTablePrefix = "custom_";

        // Act — use a distinct DbContext type to avoid EF Core model cache
        // collision with other tests that use the default prefix.
        DbContextOptionsBuilder<CustomPrefixContext> optionsBuilder = new();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using CustomPrefixContext context = new(optionsBuilder.Options);
        IEntityType? entityType = context.Model.FindEntityType(typeof(AuditEntry));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("custom_log_entries");
    }

    // -------------------------------------------------------------------------
    // ConfigureAuditingModule — key configuration
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureAuditingModule_AuditEntry_HasIdPrimaryKey()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntry));

        // Assert
        entityType.ShouldNotBeNull();
        IKey? key = entityType.FindPrimaryKey();
        key.ShouldNotBeNull();
        key.Properties.ShouldHaveSingleItem();
        key.Properties[0].Name.ShouldBe("Id");
    }

    // -------------------------------------------------------------------------
    // ConfigureAuditingModule — property constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureAuditingModule_AuditEntry_UserIdHasMaxLength256()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntry));

        // Assert
        entityType.ShouldNotBeNull();
        IProperty? property = entityType.FindProperty(nameof(AuditEntry.UserId));
        property.ShouldNotBeNull();
        property.GetMaxLength().ShouldBe(256);
    }

    [Fact]
    public void ConfigureAuditingModule_AuditEntry_IpAddressHasMaxLength45()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntry));

        // Assert
        entityType.ShouldNotBeNull();
        IProperty? property = entityType.FindProperty(nameof(AuditEntry.IpAddress));
        property.ShouldNotBeNull();
        property.GetMaxLength().ShouldBe(45);
    }

    [Fact]
    public void ConfigureAuditingModule_AuditEntry_UserAgentHasMaxLength500()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntry));

        // Assert
        entityType.ShouldNotBeNull();
        IProperty? property = entityType.FindProperty(nameof(AuditEntry.UserAgent));
        property.ShouldNotBeNull();
        property.GetMaxLength().ShouldBe(500);
    }

    [Fact]
    public void ConfigureAuditingModule_AuditEntityChange_EntityTypeHasMaxLength256()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntityChange));

        // Assert
        entityType.ShouldNotBeNull();
        IProperty? property = entityType.FindProperty(nameof(AuditEntityChange.EntityType));
        property.ShouldNotBeNull();
        property.GetMaxLength().ShouldBe(256);
    }

    [Fact]
    public void ConfigureAuditingModule_AuditPropertyChange_PropertyNameHasMaxLength256()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditPropertyChange));

        // Assert
        entityType.ShouldNotBeNull();
        IProperty? property = entityType.FindProperty(nameof(AuditPropertyChange.PropertyName));
        property.ShouldNotBeNull();
        property.GetMaxLength().ShouldBe(256);
    }

    // -------------------------------------------------------------------------
    // ConfigureAuditingModule — indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureAuditingModule_AuditEntry_HasTimestampIndex()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntry));

        // Assert
        entityType.ShouldNotBeNull();
        IIndex? index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditEntry.Timestamp))
                              && i.Properties.Count == 1);
        index.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureAuditingModule_AuditEntry_HasUserTimestampCompositeIndex()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntry));

        // Assert
        entityType.ShouldNotBeNull();
        IIndex? index = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(AuditEntry.UserId)) &&
                i.Properties.Any(p => p.Name == nameof(AuditEntry.Timestamp)) &&
                i.Properties.Count == 2);
        index.ShouldNotBeNull();
    }

    // -------------------------------------------------------------------------
    // ConfigureAuditingModule — relationships
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureAuditingModule_AuditEntry_HasCascadeDeleteToEntityChanges()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditEntityChange));

        // Assert
        entityType.ShouldNotBeNull();
        IForeignKey? fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.Properties.Any(p => p.Name == nameof(AuditEntityChange.AuditEntryId)));
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    [Fact]
    public void ConfigureAuditingModule_AuditEntityChange_HasCascadeDeleteToPropertyChanges()
    {
        // Arrange & Act
        IModel model = BuildModel();
        IEntityType? entityType = model.FindEntityType(typeof(AuditPropertyChange));

        // Assert
        entityType.ShouldNotBeNull();
        IForeignKey? fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.Properties.Any(p => p.Name == nameof(AuditPropertyChange.AuditEntityChangeId)));
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IModel BuildModel()
    {
        DbContextOptionsBuilder<TestModelContext> optionsBuilder = new();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());

        using TestModelContext context = new(optionsBuilder.Options);
        return context.Model;
    }

    private sealed class TestModelContext(DbContextOptions<TestModelContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureAuditingModule();
        }
    }

    /// <summary>
    /// Separate DbContext type used exclusively by the custom-prefix test to avoid
    /// EF Core model cache collision with <see cref="TestModelContext"/>.
    /// </summary>
    private sealed class CustomPrefixContext(DbContextOptions<CustomPrefixContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureAuditingModule();
        }
    }
}
