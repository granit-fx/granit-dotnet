using Granit.Privacy.Settings;
using Granit.Settings.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.Settings;

public sealed class PrivacySettingDefinitionProviderTests
{
    private static SettingDefinitionRegistry BuildManager()
    {
        PrivacySettingDefinitionProvider provider = new();
        return new SettingDefinitionRegistry([provider]);
    }

    public static TheoryData<string> AllSettingNames() =>
    [
        PrivacySettingNames.ControllerName,
        PrivacySettingNames.ControllerEmail,
        PrivacySettingNames.ControllerPostalAddress,
        PrivacySettingNames.DpoName,
        PrivacySettingNames.DpoEmail,
        PrivacySettingNames.SupervisoryAuthorityUrl,
    ];

    [Theory]
    [MemberData(nameof(AllSettingNames))]
    public void Setting_IsRegistered(string name) =>
        BuildManager().GetOrNull(name).ShouldNotBeNull();

    [Theory]
    [MemberData(nameof(AllSettingNames))]
    public void Setting_IsVisibleToClients(string name) =>
        BuildManager().Get(name).IsVisibleToClients.ShouldBeTrue();

    [Theory]
    [MemberData(nameof(AllSettingNames))]
    public void Setting_HasProviders_TG(string name)
    {
        SettingDefinition def = BuildManager().Get(name);

        def.Providers.ShouldContain("T");
        def.Providers.ShouldContain("G");
        def.Providers.Count.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(AllSettingNames))]
    public void Setting_HasDisplayName(string name) =>
        BuildManager().Get(name).DisplayName.ShouldNotBeNullOrWhiteSpace();

    [Theory]
    [MemberData(nameof(AllSettingNames))]
    public void Setting_HasDescription(string name) =>
        BuildManager().Get(name).Description.ShouldNotBeNullOrWhiteSpace();

    [Theory]
    [MemberData(nameof(AllSettingNames))]
    public void Setting_IsNotEncrypted(string name) =>
        BuildManager().Get(name).IsEncrypted.ShouldBeFalse();
}
