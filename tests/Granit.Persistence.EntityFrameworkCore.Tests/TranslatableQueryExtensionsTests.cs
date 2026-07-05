// =============================================================================
// Tests - TranslatableQueryExtensions
// =============================================================================
// Verifies IncludeTranslations, WhereTranslation, and OrderByTranslation
// produce correct results via EF Core InMemory.
// =============================================================================

using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class TranslatableQueryExtensionsTests
{
    // -------------------------------------------------------------------------
    // IncludeTranslations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IncludeTranslations_WithoutCulture_LoadsAll()
    {
        await using TestQueryDbContext context = await CreateAndSeedContextAsync();

        List<TestQueryDocument> results = await context.Documents
            .IncludeTranslations<TestQueryDocument, TestQueryDocTranslation>()
            .ToListAsync(TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        results.Sum(d => d.Translations.Count).ShouldBe(4,
            "all translations for all documents must be loaded");
    }

    [Fact]
    public async Task IncludeTranslations_WithCulture_LoadsOnlyMatchingCulture()
    {
        await using TestQueryDbContext context = await CreateAndSeedContextAsync();

        List<TestQueryDocument> results = await context.Documents
            .IncludeTranslations<TestQueryDocument, TestQueryDocTranslation>("fr")
            .ToListAsync(TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        var allTranslations = results.SelectMany(d => d.Translations).ToList();
        allTranslations.Count.ShouldBe(2, "only FR translations must be loaded");
        allTranslations.ShouldAllBe(t => t.Culture == "fr");
    }

    // -------------------------------------------------------------------------
    // WhereTranslation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WhereTranslation_FiltersByTranslatedProperty()
    {
        await using TestQueryDbContext context = await CreateAndSeedContextAsync();

        List<TestQueryDocument> results = await context.Documents
            .IncludeTranslations<TestQueryDocument, TestQueryDocTranslation>()
            .WhereTranslation<TestQueryDocument, TestQueryDocTranslation>(
                "fr", t => t.Title.Contains("Rapport"))
            .ToListAsync(TestContext.Current.CancellationToken);

        results.Count.ShouldBe(1);
        results[0].InternalCode.ShouldBe("DOC-001");
    }

    [Fact]
    public async Task WhereTranslation_NoCultureMatch_ReturnsEmpty()
    {
        await using TestQueryDbContext context = await CreateAndSeedContextAsync();

        List<TestQueryDocument> results = await context.Documents
            .WhereTranslation<TestQueryDocument, TestQueryDocTranslation>(
                "de", t => t.Title.Contains("Rapport"))
            .ToListAsync(TestContext.Current.CancellationToken);

        results.ShouldBeEmpty("no translation in German exists");
    }

    [Fact]
    public async Task WhereTranslation_NoPredicateMatch_ReturnsEmpty()
    {
        await using TestQueryDbContext context = await CreateAndSeedContextAsync();

        List<TestQueryDocument> results = await context.Documents
            .WhereTranslation<TestQueryDocument, TestQueryDocTranslation>(
                "fr", t => t.Title.Contains("inexistant"))
            .ToListAsync(TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // OrderByTranslation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OrderByTranslation_SortsByTranslatedProperty()
    {
        await using TestQueryDbContext context = await CreateAndSeedContextAsync();

        List<TestQueryDocument> results = await context.Documents
            .IncludeTranslations<TestQueryDocument, TestQueryDocTranslation>()
            .OrderByTranslation<TestQueryDocument, TestQueryDocTranslation, string>(
                "fr", t => t.Title)
            .ToListAsync(TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        // "Guide" comes before "Rapport" alphabetically
        results[0].InternalCode.ShouldBe("DOC-002");
        results[1].InternalCode.ShouldBe("DOC-001");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<TestQueryDbContext> CreateAndSeedContextAsync()
    {
        DbContextOptions<TestQueryDbContext> options =
            new DbContextOptionsBuilder<TestQueryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        TestQueryDbContext context = new(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var doc1Id = Guid.NewGuid();
        var doc2Id = Guid.NewGuid();

        TestQueryDocument doc1 = new() { Id = doc1Id, InternalCode = "DOC-001" };
        doc1.Translations.Add(new TestQueryDocTranslation
        {
            Id = Guid.NewGuid(),
            ParentId = doc1Id,
            Culture = "fr",
            Title = "Rapport annuel",
        });
        doc1.Translations.Add(new TestQueryDocTranslation
        {
            Id = Guid.NewGuid(),
            ParentId = doc1Id,
            Culture = "en",
            Title = "Annual Report",
        });

        TestQueryDocument doc2 = new() { Id = doc2Id, InternalCode = "DOC-002" };
        doc2.Translations.Add(new TestQueryDocTranslation
        {
            Id = Guid.NewGuid(),
            ParentId = doc2Id,
            Culture = "fr",
            Title = "Guide utilisateur",
        });
        doc2.Translations.Add(new TestQueryDocTranslation
        {
            Id = Guid.NewGuid(),
            ParentId = doc2Id,
            Culture = "en",
            Title = "User Guide",
        });

        context.Documents.AddRange(doc1, doc2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Detach all entities to force fresh loading via Include
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }

        return context;
    }
}

#region Test entities for query extensions

internal sealed class TestQueryDocument : Entity, ITranslatable<TestQueryDocTranslation>
{
    public string InternalCode { get; set; } = string.Empty;
    public ICollection<TestQueryDocTranslation> Translations { get; set; } = [];
}

internal sealed class TestQueryDocTranslation : Translation<TestQueryDocument>
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class TestQueryDbContext(DbContextOptions<TestQueryDbContext> options) : DbContext(options)
{
    public DbSet<TestQueryDocument> Documents => Set<TestQueryDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

#endregion
