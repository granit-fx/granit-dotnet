// =============================================================================
// SettingManagerTests - Tests for writes and cache invalidation
// =============================================================================
// Verifies that SettingManager writes to the store and invalidates the cache for
// the Global, Tenant and User scopes.
// =============================================================================

using Granit.Events;
using Granit.Settings.Definitions;
using Granit.Settings.Events;
using Granit.Settings.Providers;
using Granit.Settings.Services;
using Granit.Settings.Stores;
using Granit.Settings.Values;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Settings.Tests;

public sealed class SettingManagerTests
{
    private static SettingDefinitionManager ManagerWith(params SettingDefinition[] defs) =>
        new([new FakeDefinitionProvider(defs)]);

    private sealed class FakeDefinitionProvider(SettingDefinition[] definitions) : ISettingDefinitionProvider
    {
        public void Define(ISettingDefinitionContext context)
        {
            foreach (SettingDefinition def in definitions)
            {
                context.Add(def);
            }
        }
    }

    private static (SettingManager manager, InMemorySettingStore store, IFusionCache cache, ILocalEventBus eventBus)
        CreateManager(params SettingDefinition[] defs)
    {
        InMemorySettingStore store = new();
        IFusionCache cache = Substitute.For<IFusionCache>();
        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        SettingDefinitionManager defManager = ManagerWith(defs);
        SettingManager manager = new(store, store, cache, defManager, eventBus, TimeProvider.System);
        return (manager, store, cache, eventBus);
    }

    // -------------------------------------------------------------------------
    // SetGlobalAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetGlobalAsync_Writes_ToStore_WithProviderName_G()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, _, _) = CreateManager(def);

        await manager.SetGlobalAsync("App.Theme", "light", TestContext.Current.CancellationToken);

        SettingValue? stored = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored!.Value.ShouldBe("light");
    }

    [Fact]
    public async Task SetGlobalAsync_Invalidates_Cache()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, _, IFusionCache cache, _) = CreateManager(def);

        await manager.SetGlobalAsync("App.Theme", "light", TestContext.Current.CancellationToken);

        await cache.Received(1).ExpireAsync(
            Arg.Is<string>(k => k.Contains('G') && k.Contains("App.Theme")),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetGlobalAsync_UnknownSetting_Throws()
    {
        (SettingManager manager, _, _, _) = CreateManager();

        Func<Task> act = () => manager.SetGlobalAsync("Unknown.Setting", "value");

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("Unknown.Setting");
    }

    // -------------------------------------------------------------------------
    // SetForTenantAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetForTenantAsync_Writes_ToStore_WithProviderName_T_And_TenantKey()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, _, _) = CreateManager(def);
        var tenantId = Guid.NewGuid();

        await manager.SetForTenantAsync(tenantId, "App.Theme", "blue", TestContext.Current.CancellationToken);

        SettingValue? stored = await store.GetOrNullAsync(
            "App.Theme", "T", tenantId.ToString(), TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored!.Value.ShouldBe("blue");
    }

    [Fact]
    public async Task SetForTenantAsync_Invalidates_Cache_WithTenantKey()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, _, IFusionCache cache, _) = CreateManager(def);
        var tenantId = Guid.NewGuid();

        await manager.SetForTenantAsync(tenantId, "App.Theme", "blue", TestContext.Current.CancellationToken);

        await cache.Received(1).ExpireAsync(
            Arg.Is<string>(k => k.Contains('T') && k.Contains(tenantId.ToString()) && k.Contains("App.Theme")),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SetForUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetForUserAsync_Writes_ToStore_WithProviderName_U_And_UserId()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, _, _) = CreateManager(def);

        await manager.SetForUserAsync("user-42", "App.Theme", "red", TestContext.Current.CancellationToken);

        SettingValue? stored = await store.GetOrNullAsync(
            "App.Theme", "U", "user-42", TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored!.Value.ShouldBe("red");
    }

    [Fact]
    public async Task SetForUserAsync_EmptyUserId_Throws()
    {
        (SettingManager manager, _, _, _) = CreateManager(new SettingDefinition("App.Theme"));

        Func<Task> act = () => manager.SetForUserAsync("", "App.Theme", "value");

        await Should.ThrowAsync<ArgumentException>(act);
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Removes_FromStore_And_Invalidates_Cache()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, IFusionCache cache, _) = CreateManager(def);

        // Seed a value
        await store.SetAsync("App.Theme", "G", null, "light", TestContext.Current.CancellationToken);

        await manager.DeleteAsync("App.Theme", "G", null, TestContext.Current.CancellationToken);

        SettingValue? stored = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);
        stored.ShouldBeNull("deletion must remove the entry from the store");

        await cache.Received(1).ExpireAsync(
            Arg.Is<string>(k => k.Contains('G') && k.Contains("App.Theme")),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SettingChangedEvent emission
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetGlobalAsync_Publishes_SettingChangedEvent()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, _, _, ILocalEventBus eventBus) = CreateManager(def);

        await manager.SetGlobalAsync("App.Theme", "dark", TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<SettingChangedEvent>(e =>
                e.SettingName == "App.Theme" &&
                e.ProviderName == "G" &&
                e.ProviderKey == null &&
                e.OldValue == null &&
                e.NewValue == "dark"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetGlobalAsync_IncludesOldValue_WhenUpdating()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, _, ILocalEventBus eventBus) = CreateManager(def);

        await store.SetAsync("App.Theme", "G", null, "light", TestContext.Current.CancellationToken);

        await manager.SetGlobalAsync("App.Theme", "dark", TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<SettingChangedEvent>(e =>
                e.OldValue == "light" &&
                e.NewValue == "dark"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Publishes_SettingChangedEvent_WithNullNewValue()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, _, ILocalEventBus eventBus) = CreateManager(def);

        await store.SetAsync("App.Theme", "G", null, "light", TestContext.Current.CancellationToken);

        await manager.DeleteAsync("App.Theme", "G", null, TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<SettingChangedEvent>(e =>
                e.OldValue == "light" &&
                e.NewValue == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetForTenantAsync_Publishes_SettingChangedEvent_WithTenantKey()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, _, _, ILocalEventBus eventBus) = CreateManager(def);
        var tenantId = Guid.NewGuid();

        await manager.SetForTenantAsync(tenantId, "App.Theme", "blue", TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<SettingChangedEvent>(e =>
                e.ProviderName == "T" &&
                e.ProviderKey == tenantId.ToString() &&
                e.NewValue == "blue"),
            Arg.Any<CancellationToken>());
    }
}
