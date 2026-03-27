using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Settings.Definitions;
using Granit.Settings.Diagnostics;
using Granit.Settings.Events;
using Granit.Settings.Services;
using Granit.Settings.Stores;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Settings.Tests;

public sealed class SettingManagerAdditionalTests
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

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];
        public Meter Create(MeterOptions options) { Meter m = new(options); _meters.Add(m); return m; }
        public void Dispose() { foreach (Meter m in _meters) { m.Dispose(); } }
    }

    private static (SettingManager manager, InMemorySettingStore store, IFusionCache cache, ILocalEventBus eventBus)
        CreateManager(params SettingDefinition[] defs)
    {
        InMemorySettingStore store = new();
        IFusionCache cache = Substitute.For<IFusionCache>();
        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        SettingDefinitionManager defManager = ManagerWith(defs);
        SettingsMetrics metrics = new(new TestMeterFactory());
        SettingManager manager = new(store, store, cache, defManager, eventBus, TimeProvider.System, metrics);
        return (manager, store, cache, eventBus);
    }

    // -------------------------------------------------------------------------
    // SetForUserAsync — event publishing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetForUserAsync_Publishes_SettingChangedEvent_WithUserKey()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, _, _, ILocalEventBus eventBus) = CreateManager(def);

        await manager.SetForUserAsync("user-42", "App.Theme", "red", TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<SettingChangedEvent>(e =>
                e.SettingName == "App.Theme" &&
                e.ProviderName == "U" &&
                e.ProviderKey == "user-42" &&
                e.OldValue == null &&
                e.NewValue == "red"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetForUserAsync_IncludesOldValue_WhenUpdating()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, _, ILocalEventBus eventBus) = CreateManager(def);

        await store.SetAsync("App.Theme", "U", "user-42", "blue", TestContext.Current.CancellationToken);

        await manager.SetForUserAsync("user-42", "App.Theme", "red", TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<SettingChangedEvent>(e =>
                e.OldValue == "blue" &&
                e.NewValue == "red"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetForUserAsync_Invalidates_Cache()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, _, IFusionCache cache, _) = CreateManager(def);

        await manager.SetForUserAsync("user-42", "App.Theme", "red", TestContext.Current.CancellationToken);

        await cache.Received(1).ExpireAsync(
            Arg.Is<string>(k => k.Contains('U') && k.Contains("user-42") && k.Contains("App.Theme")),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetForUserAsync_NullUserId_Throws()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, _, _, _) = CreateManager(def);

        Func<Task> act = () => manager.SetForUserAsync(null!, "App.Theme", "value");

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    // -------------------------------------------------------------------------
    // SetForTenantAsync — unknown setting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetForTenantAsync_UnknownSetting_Throws()
    {
        (SettingManager manager, _, _, _) = CreateManager();

        Func<Task> act = () => manager.SetForTenantAsync(Guid.NewGuid(), "Unknown.Setting", "value");

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("Unknown.Setting");
    }

    [Fact]
    public async Task SetForUserAsync_UnknownSetting_Throws()
    {
        (SettingManager manager, _, _, _) = CreateManager();

        Func<Task> act = () => manager.SetForUserAsync("user-1", "Unknown.Setting", "value");

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("Unknown.Setting");
    }

    // -------------------------------------------------------------------------
    // DeleteAsync — event with old value from tenant/user scope
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ForTenant_Publishes_CorrectEvent()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, _, ILocalEventBus eventBus) = CreateManager(def);
        var tenantId = Guid.NewGuid();

        await store.SetAsync("App.Theme", "T", tenantId.ToString(), "blue", TestContext.Current.CancellationToken);

        await manager.DeleteAsync("App.Theme", "T", tenantId.ToString(), TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<SettingChangedEvent>(e =>
                e.ProviderName == "T" &&
                e.ProviderKey == tenantId.ToString() &&
                e.OldValue == "blue" &&
                e.NewValue == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenNoExistingValue_PublishesEventWithNullOldValue()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, _, _, ILocalEventBus eventBus) = CreateManager(def);

        await manager.DeleteAsync("App.Theme", "G", null, TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<SettingChangedEvent>(e =>
                e.OldValue == null &&
                e.NewValue == null),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SetGlobalAsync — null value clears
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetGlobalAsync_NullValue_WritesNull()
    {
        SettingDefinition def = new("App.Theme");
        (SettingManager manager, InMemorySettingStore store, _, _) = CreateManager(def);

        await manager.SetGlobalAsync("App.Theme", null, TestContext.Current.CancellationToken);

        Values.SettingValue? stored = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);

        stored.ShouldNotBeNull();
        stored!.Value.ShouldBeNull();
    }
}
