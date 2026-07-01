using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Granit.Settings.Services;
using Granit.Settings.Values;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingProviderAdditionalTests
{
    private static SettingDefinitionRegistry ManagerWith(params SettingDefinition[] defs) =>
        new([new FakeDefinitionProvider(defs)]);

    private static ISettingValueProvider MockProvider(string name, int order, SettingValue? returnValue)
    {
        ISettingValueProvider provider = Substitute.For<ISettingValueProvider>();
        provider.Name.Returns(name);
        provider.Order.Returns(order);
        provider.GetOrNullAsync(Arg.Any<SettingDefinition>(), Arg.Any<CancellationToken>())
            .Returns(returnValue);
        return provider;
    }

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

    // -------------------------------------------------------------------------
    // GetAllAsync — unknown settings
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_UnknownSetting_Returns_EntryWithNullValue()
    {
        SettingDefinitionRegistry defManager = ManagerWith();
        SettingProvider settingProvider = new(new SettingValueProviderRegistry([]), defManager);

        IReadOnlyList<SettingValue> results = await settingProvider.GetAllAsync(
            ["Unknown.Setting"], TestContext.Current.CancellationToken);

        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("Unknown.Setting");
        results[0].Value.ShouldBeNull();
        results[0].ProviderName.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task GetAllAsync_MixedKnownAndUnknown_Returns_CorrectValues()
    {
        SettingDefinition def = new("App.Theme");
        SettingDefinitionRegistry defManager = ManagerWith(def);

        ISettingValueProvider globalProvider = MockProvider("G", 300,
            new SettingValue("App.Theme", "G", null, "dark"));

        SettingValueProviderRegistry providerManager = new([globalProvider]);
        SettingProvider settingProvider = new(providerManager, defManager);

        IReadOnlyList<SettingValue> results = await settingProvider.GetAllAsync(
            ["App.Theme", "Unknown.Setting"], TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        results[0].Name.ShouldBe("App.Theme");
        results[0].Value.ShouldBe("dark");
        results[1].Name.ShouldBe("Unknown.Setting");
        results[1].Value.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // AllowList with multiple providers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AllowList_WithMultipleProviders_SkipsUnlisted()
    {
        SettingDefinition def = new("App.TenantOrGlobal");
        def.Providers.Add("T");
        def.Providers.Add("G");

        SettingDefinitionRegistry defManager = ManagerWith(def);

        ISettingValueProvider userProvider = MockProvider("U", 100,
            new SettingValue("App.TenantOrGlobal", "U", "user-1", "user-value"));
        ISettingValueProvider tenantProvider = MockProvider("T", 200, null);
        ISettingValueProvider globalProvider = MockProvider("G", 300,
            new SettingValue("App.TenantOrGlobal", "G", null, "global-value"));

        SettingValueProviderRegistry providerManager = new([userProvider, tenantProvider, globalProvider]);
        SettingProvider settingProvider = new(providerManager, defManager);

        string? result = await settingProvider.GetOrNullAsync("App.TenantOrGlobal", TestContext.Current.CancellationToken);

        result.ShouldBe("global-value");
        await userProvider.DidNotReceive().GetOrNullAsync(Arg.Any<SettingDefinition>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Empty providers list
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NoProviders_RegisteredSetting_Returns_Null()
    {
        SettingDefinition def = new("App.Theme");
        SettingDefinitionRegistry defManager = ManagerWith(def);
        SettingProvider settingProvider = new(new SettingValueProviderRegistry([]), defManager);

        string? result = await settingProvider.GetOrNullAsync("App.Theme", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // GetAllAsync — empty input
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_EmptyNames_Returns_EmptyList()
    {
        SettingDefinitionRegistry defManager = ManagerWith();
        SettingProvider settingProvider = new(new SettingValueProviderRegistry([]), defManager);

        IReadOnlyList<SettingValue> results = await settingProvider.GetAllAsync(
            [], TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }
}
