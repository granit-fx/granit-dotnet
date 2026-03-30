// =============================================================================
// Tests - Translation Convention in ApplyGranitConventions
// =============================================================================
// Verifies that ApplyGranitConventions detects ITranslation<TParent> and
// configures: FK with cascade delete, unique index (ParentId, Culture),
// Culture max length 20.
//
// Uses InMemory database. Unique index enforcement is verified via model
// metadata (InMemory does not enforce unique indexes at runtime).
// =============================================================================

using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class TranslationConventionTests
{
    // -------------------------------------------------------------------------
    // Convention detection — Translation<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_TranslationEntity_HasForeignKeyToParent()
    {
        using TestTranslationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestDocTranslation));

        entityType.ShouldNotBeNull();

        IForeignKey? fk = entityType!.GetForeignKeys().FirstOrDefault();
        fk.ShouldNotBeNull("a FK to the parent entity must be configured");
        fk!.PrincipalEntityType.ClrType.ShouldBe(typeof(TestDocument));
        fk.Properties.ShouldContain(p => p.Name == nameof(ITranslation.ParentId));
    }

    [Fact]
    public void ApplyGranitConventions_TranslationEntity_CascadeDeleteFromParent()
    {
        using TestTranslationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestDocTranslation));
        IForeignKey? fk = entityType!.GetForeignKeys().FirstOrDefault();

        fk.ShouldNotBeNull();
        fk!.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    [Fact]
    public void ApplyGranitConventions_TranslationEntity_HasUniqueIndexOnParentIdCulture()
    {
        using TestTranslationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestDocTranslation));

        IIndex? uniqueIndex = entityType!.GetIndexes()
            .FirstOrDefault(i => i.IsUnique
                && i.Properties.Any(p => p.Name == nameof(ITranslation.ParentId))
                && i.Properties.Any(p => p.Name == nameof(ITranslation.Culture)));

        uniqueIndex.ShouldNotBeNull("a unique index on (ParentId, Culture) must exist");
    }

    [Fact]
    public void ApplyGranitConventions_TranslationEntity_CultureMaxLength20()
    {
        using TestTranslationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestDocTranslation));
        IProperty? cultureProperty = entityType!.FindProperty(nameof(ITranslation.Culture));

        cultureProperty.ShouldNotBeNull();
        cultureProperty!.GetMaxLength().ShouldBe(20);
    }

    // -------------------------------------------------------------------------
    // Convention detection — AuditedTranslation<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_AuditedTranslation_AlsoConfigured()
    {
        using TestAuditedTranslationDbContext context = CreateAuditedContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestAuditedDocTranslation));

        entityType.ShouldNotBeNull();

        IForeignKey? fk = entityType!.GetForeignKeys().FirstOrDefault();
        fk.ShouldNotBeNull("FK must be configured for AuditedTranslation<T>");
        fk!.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);

        IIndex? uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.IsUnique
                && i.Properties.Any(p => p.Name == nameof(ITranslation.ParentId))
                && i.Properties.Any(p => p.Name == nameof(ITranslation.Culture)));

        uniqueIndex.ShouldNotBeNull("unique index on (ParentId, Culture) must exist for AuditedTranslation");
    }

    // -------------------------------------------------------------------------
    // Non-translation entities are not affected
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_NonTranslationEntity_NotAffected()
    {
        using TestTranslationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestDocument));

        entityType.ShouldNotBeNull();
        entityType!.GetIndexes().ShouldBeEmpty("parent entity should not get translation indexes");
    }

    // -------------------------------------------------------------------------
    // Cascade delete integration (InMemory)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CascadeDelete_WhenParentDeleted_TranslationsRemoved()
    {
        // Arrange
        await using TestTranslationDbContext context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        TestDocument document = new() { Id = Guid.NewGuid() };
        document.Translations.Add(new TestDocTranslation
        {
            Id = Guid.NewGuid(),
            ParentId = document.Id,
            Culture = "fr",
            Title = "Titre",
        });
        document.Translations.Add(new TestDocTranslation
        {
            Id = Guid.NewGuid(),
            ParentId = document.Id,
            Culture = "en",
            Title = "Title",
        });
        context.Documents.Add(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        context.Documents.Remove(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        List<TestDocTranslation> remainingTranslations = await context.Set<TestDocTranslation>()
            .ToListAsync(TestContext.Current.CancellationToken);
        remainingTranslations.ShouldBeEmpty("translations must be cascade-deleted with parent");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TestTranslationDbContext CreateContext()
    {
        DbContextOptions<TestTranslationDbContext> options =
            new DbContextOptionsBuilder<TestTranslationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestTranslationDbContext(options);
    }

    private static TestAuditedTranslationDbContext CreateAuditedContext()
    {
        DbContextOptions<TestAuditedTranslationDbContext> options =
            new DbContextOptionsBuilder<TestAuditedTranslationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestAuditedTranslationDbContext(options);
    }
}

#region Test entities

internal sealed class TestDocument : Entity, ITranslatable<TestDocTranslation>
{
    public ICollection<TestDocTranslation> Translations { get; set; } = [];
}

internal sealed class TestDocTranslation : Translation<TestDocument>
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class TestAuditedDoc : AuditedEntity, ITranslatable<TestAuditedDocTranslation>
{
    public ICollection<TestAuditedDocTranslation> Translations { get; set; } = [];
}

internal sealed class TestAuditedDocTranslation : AuditedTranslation<TestAuditedDoc>
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class TestTranslationDbContext(DbContextOptions<TestTranslationDbContext> options) : DbContext(options)
{
    public DbSet<TestDocument> Documents => Set<TestDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

internal sealed class TestAuditedTranslationDbContext(DbContextOptions<TestAuditedTranslationDbContext> options) : DbContext(options)
{
    public DbSet<TestAuditedDoc> Documents => Set<TestAuditedDoc>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

#endregion
