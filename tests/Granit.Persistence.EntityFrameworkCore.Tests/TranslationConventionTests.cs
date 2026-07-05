// =============================================================================
// Tests - Translation Convention in ApplyGranitConventions
// =============================================================================
// Verifies that ApplyGranitConventions detects ITranslation<TParent> and
// configures: FK with cascade delete, unique index (ParentId, Culture),
// Culture max length 20, and mirrors parent ISoftDeletable / IActive filters.
//
// Uses InMemory database. Unique index enforcement is verified via model
// metadata (InMemory does not enforce unique indexes at runtime).
// =============================================================================

using System.Linq.Expressions;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class TranslationConventionTests
{
    // EF Core caches the model per DbContext type. The filter proxy captures the IDataFilter
    // instance at model-build time — subsequent filter evaluations read from that same instance.
    // Static instances guarantee that the captured proxy references the object mutated by tests.
    private static readonly TranslationTestDataFilter SharedTranslationDataFilter = new();

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
    // Filter mirrors — ISoftDeletable parent
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_SoftDeletableParent_TranslationHasSoftDeleteFilter()
    {
        using TestSoftDeletableTranslationDbContext context = CreateSoftDeletableContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestSoftDeletableDocTranslation));

        entityType.ShouldNotBeNull();
        entityType!.GetDeclaredQueryFilters()
            .ShouldContain(f => f.Key == GranitFilterNames.SoftDelete,
                "translation of a soft-deletable parent must inherit the SoftDelete filter");
    }

    [Fact]
    public void ApplyGranitConventions_SoftDeletableParent_TranslationFilter_ExcludesWhenParentDeleted()
    {
        SharedTranslationDataFilter.SetEnabled<ISoftDeletable>(true);

        using TestSoftDeletableTranslationDbContextWithFilter context = CreateSoftDeletableContextWithFilter();
        LambdaExpression? filter = context.Model
            .FindEntityType(typeof(TestFilteredSoftDeletableDocTranslation))
            ?.GetDeclaredQueryFilters()
            .FirstOrDefault(f => f.Key == GranitFilterNames.SoftDelete)?.Expression;
        filter.ShouldNotBeNull();

        var compiled = (Func<TestFilteredSoftDeletableDocTranslation, bool>)filter!.Compile();

        // Null parent → passes (null guard)
        compiled(new TestFilteredSoftDeletableDocTranslation { Parent = null }).ShouldBeTrue("null parent must pass null guard");

        // Non-deleted parent → passes
        compiled(new TestFilteredSoftDeletableDocTranslation
        {
            Parent = new TestFilteredSoftDeletableDocument { IsDeleted = false },
        }).ShouldBeTrue("translation of non-deleted parent must pass");

        // Deleted parent → filtered
        compiled(new TestFilteredSoftDeletableDocTranslation
        {
            Parent = new TestFilteredSoftDeletableDocument { IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow, DeletedBy = "dpo" },
        }).ShouldBeFalse("translation of deleted parent must be filtered");

        // Bypass via IDataFilter
        SharedTranslationDataFilter.SetEnabled<ISoftDeletable>(false);
        compiled(new TestFilteredSoftDeletableDocTranslation
        {
            Parent = new TestFilteredSoftDeletableDocument { IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow, DeletedBy = "dpo" },
        }).ShouldBeTrue("filter disabled via IDataFilter — deleted parent must pass");

        SharedTranslationDataFilter.SetEnabled<ISoftDeletable>(true);
    }

    // -------------------------------------------------------------------------
    // Filter mirrors — IActive parent
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_ActiveParent_TranslationHasActiveFilter()
    {
        using TestActiveTranslationDbContext context = CreateActiveContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestActiveDocTranslation));

        entityType.ShouldNotBeNull();
        entityType!.GetDeclaredQueryFilters()
            .ShouldContain(f => f.Key == GranitFilterNames.Active,
                "translation of an active-filtered parent must inherit the Active filter");
    }

    [Fact]
    public void ApplyGranitConventions_ActiveParent_TranslationFilter_ExcludesWhenParentInactive()
    {
        SharedTranslationDataFilter.SetEnabled<IActive>(true);

        using TestActiveTranslationDbContextWithFilter context = CreateActiveContextWithFilter();
        LambdaExpression? filter = context.Model
            .FindEntityType(typeof(TestFilteredActiveDocTranslation))
            ?.GetDeclaredQueryFilters()
            .FirstOrDefault(f => f.Key == GranitFilterNames.Active)?.Expression;
        filter.ShouldNotBeNull();

        var compiled = (Func<TestFilteredActiveDocTranslation, bool>)filter!.Compile();

        // Null parent → passes
        compiled(new TestFilteredActiveDocTranslation { Parent = null }).ShouldBeTrue("null parent must pass null guard");

        // Active parent → passes
        compiled(new TestFilteredActiveDocTranslation
        {
            Parent = new TestFilteredActiveDocument { Activated = true },
        }).ShouldBeTrue("translation of active parent must pass");

        // Inactive parent → filtered
        compiled(new TestFilteredActiveDocTranslation
        {
            Parent = new TestFilteredActiveDocument { Activated = false },
        }).ShouldBeFalse("translation of inactive parent must be filtered");

        // Bypass via IDataFilter
        SharedTranslationDataFilter.SetEnabled<IActive>(false);
        compiled(new TestFilteredActiveDocTranslation
        {
            Parent = new TestFilteredActiveDocument { Activated = false },
        }).ShouldBeTrue("filter disabled via IDataFilter — inactive parent must pass");

        SharedTranslationDataFilter.SetEnabled<IActive>(true);
    }

    // -------------------------------------------------------------------------
    // No filter mirror when parent is a plain entity
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_PlainParent_TranslationHasNoQueryFilters()
    {
        using TestTranslationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestDocTranslation));

        entityType.ShouldNotBeNull();
        entityType!.GetDeclaredQueryFilters().ShouldBeEmpty(
            "translation of a plain entity should not have any query filters");
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

    private static TestSoftDeletableTranslationDbContext CreateSoftDeletableContext()
    {
        DbContextOptions<TestSoftDeletableTranslationDbContext> options =
            new DbContextOptionsBuilder<TestSoftDeletableTranslationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestSoftDeletableTranslationDbContext(options);
    }

    private static TestSoftDeletableTranslationDbContextWithFilter CreateSoftDeletableContextWithFilter()
    {
        DbContextOptions<TestSoftDeletableTranslationDbContextWithFilter> options =
            new DbContextOptionsBuilder<TestSoftDeletableTranslationDbContextWithFilter>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestSoftDeletableTranslationDbContextWithFilter(options, SharedTranslationDataFilter);
    }

    private static TestActiveTranslationDbContext CreateActiveContext()
    {
        DbContextOptions<TestActiveTranslationDbContext> options =
            new DbContextOptionsBuilder<TestActiveTranslationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestActiveTranslationDbContext(options);
    }

    private static TestActiveTranslationDbContextWithFilter CreateActiveContextWithFilter()
    {
        DbContextOptions<TestActiveTranslationDbContextWithFilter> options =
            new DbContextOptionsBuilder<TestActiveTranslationDbContextWithFilter>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestActiveTranslationDbContextWithFilter(options, SharedTranslationDataFilter);
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

// --- ISoftDeletable parent (metadata test — no dataFilter) ---

internal sealed class TestSoftDeletableDocument : Entity, ISoftDeletable
{
    public ICollection<TestSoftDeletableDocTranslation> Translations { get; set; } = [];
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

internal sealed class TestSoftDeletableDocTranslation : Translation<TestSoftDeletableDocument>
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class TestSoftDeletableTranslationDbContext(DbContextOptions<TestSoftDeletableTranslationDbContext> options) : DbContext(options)
{
    public DbSet<TestSoftDeletableDocument> Documents => Set<TestSoftDeletableDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

// --- ISoftDeletable parent (compiled filter test — with dataFilter) ---

internal sealed class TestFilteredSoftDeletableDocument : Entity, ISoftDeletable
{
    public ICollection<TestFilteredSoftDeletableDocTranslation> Translations { get; set; } = [];
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

internal sealed class TestFilteredSoftDeletableDocTranslation : Translation<TestFilteredSoftDeletableDocument>
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class TestSoftDeletableTranslationDbContextWithFilter(
    DbContextOptions<TestSoftDeletableTranslationDbContextWithFilter> options,
    IDataFilter dataFilter) : DbContext(options)
{
    private readonly IDataFilter _dataFilter = dataFilter;

    public DbSet<TestFilteredSoftDeletableDocument> Documents => Set<TestFilteredSoftDeletableDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions(dataFilter: _dataFilter);
}

// --- IActive parent (metadata test — no dataFilter) ---

internal sealed class TestActiveDocument : Entity, IActive
{
    public ICollection<TestActiveDocTranslation> Translations { get; set; } = [];
    public bool Activated { get; set; }
}

internal sealed class TestActiveDocTranslation : Translation<TestActiveDocument>
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class TestActiveTranslationDbContext(DbContextOptions<TestActiveTranslationDbContext> options) : DbContext(options)
{
    public DbSet<TestActiveDocument> Documents => Set<TestActiveDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

// --- IActive parent (compiled filter test — with dataFilter) ---

internal sealed class TestFilteredActiveDocument : Entity, IActive
{
    public ICollection<TestFilteredActiveDocTranslation> Translations { get; set; } = [];
    public bool Activated { get; set; }
}

internal sealed class TestFilteredActiveDocTranslation : Translation<TestFilteredActiveDocument>
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class TestActiveTranslationDbContextWithFilter(
    DbContextOptions<TestActiveTranslationDbContextWithFilter> options,
    IDataFilter dataFilter) : DbContext(options)
{
    private readonly IDataFilter _dataFilter = dataFilter;

    public DbSet<TestFilteredActiveDocument> Documents => Set<TestFilteredActiveDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions(dataFilter: _dataFilter);
}

// Minimal IDataFilter for filter expression tests — direct state control without AsyncLocal.
internal sealed class TranslationTestDataFilter : IDataFilter
{
    private readonly Dictionary<Type, bool> _state = [];

    public void SetEnabled<TFilter>(bool enabled) => _state[typeof(TFilter)] = enabled;

    public IDisposable Disable<TFilter>() where TFilter : class
    {
        _state[typeof(TFilter)] = false;
        return NullScope.Instance;
    }

    public IDisposable Enable<TFilter>() where TFilter : class
    {
        _state[typeof(TFilter)] = true;
        return NullScope.Instance;
    }

    public bool IsEnabled<TFilter>() where TFilter : class =>
        !_state.TryGetValue(typeof(TFilter), out bool value) || value;

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

#endregion Test entities
