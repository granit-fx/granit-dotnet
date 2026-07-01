// =============================================================================
// Tests - EfCoreSettingStore
// =============================================================================
// Verifies CRUD operations via InMemory EF provider.
// Each test uses an isolated database name to prevent state leakage.
// =============================================================================

using Granit.Encryption;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Settings.Definitions;
using Granit.Settings.Domain;
using Granit.Settings.EntityFrameworkCore.Internal;
using Granit.Settings.Values;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Settings.EntityFrameworkCore.Tests;

public sealed class EfCoreSettingStoreTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private sealed class TestSettingsDbContextFactory(string dbName)
        : IDbContextFactory<SettingsDbContext>
    {
        private readonly DbContextOptions<SettingsDbContext> _options = new DbContextOptionsBuilder<SettingsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        public SettingsDbContext CreateDbContext()
            => new(_options, GranitDesignTime.CurrentTenant);
    }

    private sealed class PassthroughEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string? Decrypt(string cipherText) => cipherText;
    }

    private static EfCoreSettingStore CreateStore(string dbName)
        => new(new TestSettingsDbContextFactory(dbName), new SettingDefinitionRegistry([]), new PassthroughEncryption());

    private static async Task SeedAsync(
        string dbName,
        string name,
        string providerName,
        string? providerKey,
        string? value,
        CancellationToken cancellationToken = default)
    {
        TestSettingsDbContextFactory factory = new(dbName);
        await using SettingsDbContext context = factory.CreateDbContext();

        context.SettingRecords.Add(new SettingRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            ProviderName = providerName,
            ProviderKey = providerKey,
            Value = value,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed",
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // GetOrNullAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrNullAsync_ExistingRecord_ReturnsSettingValue()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "App.Theme", "G", null, "dark",
            TestContext.Current.CancellationToken);

        EfCoreSettingStore store = CreateStore(db);
        SettingValue? result = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("App.Theme");
        result.ProviderName.ShouldBe("G");
        result.ProviderKey.ShouldBeNull();
        result.Value.ShouldBe("dark");
    }

    [Fact]
    public async Task GetOrNullAsync_NoRecord_ReturnsNull()
    {
        EfCoreSettingStore store = CreateStore(Guid.NewGuid().ToString());

        SettingValue? result = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrNullAsync_FiltersBy_NameProviderNameProviderKey()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        await SeedAsync(db, "App.Theme", "G", null, "dark-global",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, "App.Theme", "T", tenantId.ToString(), "light-tenant",
            TestContext.Current.CancellationToken);

        EfCoreSettingStore store = CreateStore(db);
        SettingValue? tenantResult = await store.GetOrNullAsync(
            "App.Theme", "T", tenantId.ToString(), TestContext.Current.CancellationToken);

        tenantResult!.Value.ShouldBe("light-tenant");
    }

    // -------------------------------------------------------------------------
    // GetListAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_ReturnsAllForProviderAndKey()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        await SeedAsync(db, "App.Theme", "T", tenantId.ToString(), "dark",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, "App.Language", "T", tenantId.ToString(), "fr",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, "App.Theme", "G", null, "light",
            TestContext.Current.CancellationToken); // different provider — must not appear

        EfCoreSettingStore store = CreateStore(db);
        IReadOnlyList<SettingValue> result = await store.GetListAsync(
            "T", tenantId.ToString(), TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain(v => v.Name == "App.Theme" && v.Value == "dark");
        result.ShouldContain(v => v.Name == "App.Language" && v.Value == "fr");
    }

    [Fact]
    public async Task GetListAsync_Empty_ReturnsEmptyList()
    {
        EfCoreSettingStore store = CreateStore(Guid.NewGuid().ToString());

        IReadOnlyList<SettingValue> result = await store.GetListAsync(
            "G", null, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // SetAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetAsync_NewRecord_Persists()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreSettingStore store = CreateStore(db);

        await store.SetAsync("App.Theme", "G", null, "dark",
            TestContext.Current.CancellationToken);

        SettingValue? result = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);

        result!.Value.ShouldBe("dark");
    }

    [Fact]
    public async Task SetAsync_ExistingRecord_UpdatesValue()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "App.Theme", "G", null, "dark",
            TestContext.Current.CancellationToken);

        EfCoreSettingStore store = CreateStore(db);
        await store.SetAsync("App.Theme", "G", null, "light",
            TestContext.Current.CancellationToken);

        SettingValue? result = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);

        result!.Value.ShouldBe("light");
    }

    [Fact]
    public async Task SetAsync_IsIdempotent_NoDuplicates()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreSettingStore store = CreateStore(db);

        await store.SetAsync("App.Theme", "G", null, "dark",
            TestContext.Current.CancellationToken);
        await store.SetAsync("App.Theme", "G", null, "dark",
            TestContext.Current.CancellationToken);

        IReadOnlyList<SettingValue> all = await store.GetListAsync(
            "G", null, TestContext.Current.CancellationToken);

        all.Where(v => v.Name == "App.Theme").Count().ShouldBe(1,
            "upsert must not create duplicate records");
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ExistingRecord_Removes()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "App.Theme", "G", null, "dark",
            TestContext.Current.CancellationToken);

        EfCoreSettingStore store = CreateStore(db);
        await store.DeleteAsync("App.Theme", "G", null,
            TestContext.Current.CancellationToken);

        SettingValue? result = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_UnknownRecord_DoesNotThrow()
    {
        EfCoreSettingStore store = CreateStore(Guid.NewGuid().ToString());

        Func<Task> act = () => store.DeleteAsync(
            "Ghost.Setting", "G", null, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task DeleteAsync_LeavesOtherRecordsIntact()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "App.Theme", "G", null, "dark",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, "App.Language", "G", null, "fr",
            TestContext.Current.CancellationToken);

        EfCoreSettingStore store = CreateStore(db);
        await store.DeleteAsync("App.Theme", "G", null,
            TestContext.Current.CancellationToken);

        SettingValue? language = await store.GetOrNullAsync(
            "App.Language", "G", null, TestContext.Current.CancellationToken);

        language!.Value.ShouldBe("fr", "App.Language must not be affected");
    }
}
