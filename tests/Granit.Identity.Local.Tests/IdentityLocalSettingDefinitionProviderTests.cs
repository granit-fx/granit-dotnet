using Granit.Identity.Local.Internal;
using Granit.Settings.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Tests;

public sealed class IdentityLocalSettingDefinitionProviderTests
{
    private static SettingDefinitionManager BuildManager()
    {
        IdentityLocalSettingDefinitionProvider provider = new();
        return new SettingDefinitionManager([provider]);
    }

    [Fact]
    public void Defines_AllowSelfRegistration_Setting()
    {
        SettingDefinitionManager manager = BuildManager();

        SettingDefinition? def = manager.GetOrNull(IdentityLocalSettingNames.AllowSelfRegistration);

        def.ShouldNotBeNull();
    }

    [Fact]
    public void AllowSelfRegistration_DefaultValue_IsFalse() =>
        BuildManager()
            .Get(IdentityLocalSettingNames.AllowSelfRegistration)
            .DefaultValue
            .ShouldBe("false");

    [Fact]
    public void AllowSelfRegistration_IsVisibleToClients() =>
        BuildManager()
            .Get(IdentityLocalSettingNames.AllowSelfRegistration)
            .IsVisibleToClients
            .ShouldBeTrue();

    [Fact]
    public void AllowSelfRegistration_HasProviders_TG()
    {
        SettingDefinition def = BuildManager()
            .Get(IdentityLocalSettingNames.AllowSelfRegistration);

        def.Providers.ShouldContain("T");
        def.Providers.ShouldContain("G");
        def.Providers.Count.ShouldBe(2);
    }

    [Fact]
    public void AllowSelfRegistration_HasDisplayName() =>
        BuildManager()
            .Get(IdentityLocalSettingNames.AllowSelfRegistration)
            .DisplayName
            .ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void AllowSelfRegistration_HasDescription() =>
        BuildManager()
            .Get(IdentityLocalSettingNames.AllowSelfRegistration)
            .Description
            .ShouldNotBeNullOrWhiteSpace();
}
