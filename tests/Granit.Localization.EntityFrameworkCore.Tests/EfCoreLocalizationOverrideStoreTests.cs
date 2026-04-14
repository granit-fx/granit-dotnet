using Granit.Localization.EntityFrameworkCore.Entities;
using Granit.Localization.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Localization.EntityFrameworkCore.Tests;

public sealed class EfCoreLocalizationOverrideStoreTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private sealed class InMemoryContextFactory(string dbName)
        : IDbContextFactory<LocalizationDbContext>
    {
        public LocalizationDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<LocalizationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options);

        public Task<LocalizationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static EfCoreLocalizationOverrideStore CreateStore(string dbName) =>
        new(new InMemoryContextFactory(dbName), Substitute.For<ICurrentTenant>());

    private static async Task SeedAsync(
        string dbName,
        string resourceName,
        string culture,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        InMemoryContextFactory factory = new(dbName);
        await using LocalizationDbContext ctx = factory.CreateDbContext();
        ctx.LocalizationOverrides.Add(new LocalizationOverride
        {
            Id = Guid.NewGuid(),
            ResourceName = resourceName,
            CultureName = culture,
            Key = key,
            Value = value,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed",
        });
        await ctx.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // GetOverridesAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOverridesAsync_WithExistingEntries_ReturnsDictionary()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "TestApp", "fr", "Patient.Title", "Patient",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, "TestApp", "fr", "Doctor.Title", "Médecin",
            TestContext.Current.CancellationToken);

        EfCoreLocalizationOverrideStore store = CreateStore(db);
        IReadOnlyDictionary<string, string> result =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result["Patient.Title"].ShouldBe("Patient");
        result["Doctor.Title"].ShouldBe("Médecin");
    }

    [Fact]
    public async Task GetOverridesAsync_NoneExist_ReturnsEmptyDictionary()
    {
        EfCoreLocalizationOverrideStore store = CreateStore(Guid.NewGuid().ToString());
        IReadOnlyDictionary<string, string> result =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetOverridesAsync_FiltersBy_ResourceAndCulture()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "TestApp", "fr", "Key1", "Valeur1",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, "TestApp", "en", "Key1", "Value1",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, "OtherResource", "fr", "Key1", "Autre",
            TestContext.Current.CancellationToken);

        EfCoreLocalizationOverrideStore store = CreateStore(db);
        IReadOnlyDictionary<string, string> result =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1, "only the fr culture of TestApp resource should be returned");
        result["Key1"].ShouldBe("Valeur1");
    }

    // -------------------------------------------------------------------------
    // SetOverrideAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetOverrideAsync_NewKey_PersistsValue()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreLocalizationOverrideStore store = CreateStore(db);

        await store.SetOverrideAsync("TestApp", "fr", "Patient.Title", "Patient personnalisé",
            TestContext.Current.CancellationToken);

        IReadOnlyDictionary<string, string> result =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        result["Patient.Title"].ShouldBe("Patient personnalisé");
    }

    [Fact]
    public async Task SetOverrideAsync_ExistingKey_UpdatesValue()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "TestApp", "fr", "Patient.Title", "Patient",
            TestContext.Current.CancellationToken);

        EfCoreLocalizationOverrideStore store = CreateStore(db);
        await store.SetOverrideAsync("TestApp", "fr", "Patient.Title", "Bénéficiaire",
            TestContext.Current.CancellationToken);

        IReadOnlyDictionary<string, string> result =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        result["Patient.Title"].ShouldBe("Bénéficiaire", "upsert should update the existing value");
    }

    [Fact]
    public async Task SetOverrideAsync_IsIdempotent_WhenCalledTwiceWithSameValue()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreLocalizationOverrideStore store = CreateStore(db);

        await store.SetOverrideAsync("TestApp", "fr", "Key", "Valeur",
            TestContext.Current.CancellationToken);
        await store.SetOverrideAsync("TestApp", "fr", "Key", "Valeur",
            TestContext.Current.CancellationToken);

        InMemoryContextFactory factory = new(db);
        await using LocalizationDbContext ctx = factory.CreateDbContext();
        int count = await ctx.LocalizationOverrides.CountAsync(
            o => o.ResourceName == "TestApp" && o.CultureName == "fr" && o.Key == "Key",
            TestContext.Current.CancellationToken);

        count.ShouldBe(1, "upsert must not create duplicates");
    }

    // -------------------------------------------------------------------------
    // RemoveOverrideAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveOverrideAsync_ExistingKey_RemovesRow()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "TestApp", "fr", "Patient.Title", "Patient",
            TestContext.Current.CancellationToken);

        EfCoreLocalizationOverrideStore store = CreateStore(db);
        await store.RemoveOverrideAsync("TestApp", "fr", "Patient.Title",
            TestContext.Current.CancellationToken);

        IReadOnlyDictionary<string, string> result =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveOverrideAsync_UnknownKey_DoesNotThrow()
    {
        EfCoreLocalizationOverrideStore store = CreateStore(Guid.NewGuid().ToString());

        Func<Task> act = () => store.RemoveOverrideAsync(
            "TestApp", "fr", "Ghost.Key", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task RemoveOverrideAsync_LeavesOtherKeysIntact()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "TestApp", "fr", "Key1", "Valeur1",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, "TestApp", "fr", "Key2", "Valeur2",
            TestContext.Current.CancellationToken);

        EfCoreLocalizationOverrideStore store = CreateStore(db);
        await store.RemoveOverrideAsync("TestApp", "fr", "Key1",
            TestContext.Current.CancellationToken);

        IReadOnlyDictionary<string, string> result =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result["Key2"].ShouldBe("Valeur2", "Key2 must not be affected");
    }
}
