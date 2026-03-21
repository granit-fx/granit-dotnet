using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class WellKnownSettingDefinitionProviderTests
{
    private static SettingDefinitionManager BuildManager()
    {
        WellKnownSettingDefinitionProvider provider = new();
        return new SettingDefinitionManager([provider]);
    }

    [Fact]
    public void Defines_PreferredCulture_Setting()
    {
        SettingDefinitionManager manager = BuildManager();

        SettingDefinition? def = manager.GetOrNull(WellKnownSettingNames.PreferredCulture);

        def.ShouldNotBeNull();
    }

    [Fact]
    public void Defines_PreferredTimezone_Setting()
    {
        SettingDefinitionManager manager = BuildManager();

        SettingDefinition? def = manager.GetOrNull(WellKnownSettingNames.PreferredTimezone);

        def.ShouldNotBeNull();
    }

    [Fact]
    public void PreferredCulture_IsVisibleToClients()
    {
        SettingDefinitionManager manager = BuildManager();
        SettingDefinition def = manager.Get(WellKnownSettingNames.PreferredCulture);

        def.IsVisibleToClients.ShouldBeTrue();
    }

    [Fact]
    public void PreferredTimezone_IsVisibleToClients()
    {
        SettingDefinitionManager manager = BuildManager();
        SettingDefinition def = manager.Get(WellKnownSettingNames.PreferredTimezone);

        def.IsVisibleToClients.ShouldBeTrue();
    }

    [Fact]
    public void PreferredCulture_HasProviders_UTG()
    {
        SettingDefinitionManager manager = BuildManager();
        SettingDefinition def = manager.Get(WellKnownSettingNames.PreferredCulture);

        def.Providers.ShouldContain("U");
        def.Providers.ShouldContain("T");
        def.Providers.ShouldContain("G");
        def.Providers.Count.ShouldBe(3);
    }

    [Fact]
    public void PreferredTimezone_HasProviders_UTG()
    {
        SettingDefinitionManager manager = BuildManager();
        SettingDefinition def = manager.Get(WellKnownSettingNames.PreferredTimezone);

        def.Providers.ShouldContain("U");
        def.Providers.ShouldContain("T");
        def.Providers.ShouldContain("G");
        def.Providers.Count.ShouldBe(3);
    }

    [Fact]
    public void PreferredCulture_HasDisplayName()
    {
        SettingDefinitionManager manager = BuildManager();
        SettingDefinition def = manager.Get(WellKnownSettingNames.PreferredCulture);

        def.DisplayName.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PreferredTimezone_HasDescription()
    {
        SettingDefinitionManager manager = BuildManager();
        SettingDefinition def = manager.Get(WellKnownSettingNames.PreferredTimezone);

        def.Description.ShouldNotBeNullOrWhiteSpace();
    }
}
