using Granit.Privacy.Notifications.GlobalContexts;
using Granit.Privacy.Settings;
using Granit.Settings.Services;
using Granit.Settings.Values;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.GlobalContexts;

public sealed class PrivacyContactGlobalContextTests
{
    [Fact]
    public void ContextName_IsPrivacy() =>
        BuildContext(provider: Substitute.For<ISettingProvider>())
            .ContextName.ShouldBe("privacy");

    [Fact]
    public async Task Resolve_AllSettingsPresent_ProjectsAllFields()
    {
        ISettingProvider provider = StubProvider(new Dictionary<string, string?>
        {
            [PrivacySettingNames.ControllerName] = "Acme Corp",
            [PrivacySettingNames.ControllerEmail] = "privacy@acme.test",
            [PrivacySettingNames.ControllerPostalAddress] = "1 rue de la Loi, 1000 Bruxelles",
            [PrivacySettingNames.DpoName] = "Jane Doe",
            [PrivacySettingNames.DpoEmail] = "dpo@acme.test",
            [PrivacySettingNames.SupervisoryAuthorityUrl] = "https://www.autoriteprotectiondonnees.be",
        });

        dynamic resolved = await BuildContext(provider).ResolveAsync(TestContext.Current.CancellationToken);

        ((string)resolved.controller_name).ShouldBe("Acme Corp");
        ((string)resolved.controller_email).ShouldBe("privacy@acme.test");
        ((string)resolved.controller_postal_address).ShouldBe("1 rue de la Loi, 1000 Bruxelles");
        ((string)resolved.dpo_name).ShouldBe("Jane Doe");
        ((string)resolved.dpo_email).ShouldBe("dpo@acme.test");
        ((string)resolved.supervisory_authority_url).ShouldBe("https://www.autoriteprotectiondonnees.be");
    }

    [Fact]
    public async Task Resolve_MissingSettings_ProjectsEmptyStrings()
    {
        ISettingProvider provider = StubProvider([]);

        dynamic resolved = await BuildContext(provider).ResolveAsync(TestContext.Current.CancellationToken);

        ((string)resolved.controller_name).ShouldBeEmpty();
        ((string)resolved.controller_email).ShouldBeEmpty();
        ((string)resolved.dpo_name).ShouldBeEmpty();
        ((string)resolved.dpo_email).ShouldBeEmpty();
        ((string)resolved.supervisory_authority_url).ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_OnlyControllerSet_DpoFieldsAreEmpty()
    {
        ISettingProvider provider = StubProvider(new Dictionary<string, string?>
        {
            [PrivacySettingNames.ControllerName] = "Acme Corp",
            [PrivacySettingNames.ControllerEmail] = "privacy@acme.test",
        });

        dynamic resolved = await BuildContext(provider).ResolveAsync(TestContext.Current.CancellationToken);

        ((string)resolved.controller_name).ShouldBe("Acme Corp");
        ((string)resolved.dpo_name).ShouldBeEmpty();
        ((string)resolved.dpo_email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_NullSettingValue_ProjectsEmptyString()
    {
        ISettingProvider provider = StubProvider(new Dictionary<string, string?>
        {
            [PrivacySettingNames.ControllerName] = null,
        });

        dynamic resolved = await BuildContext(provider).ResolveAsync(TestContext.Current.CancellationToken);

        ((string)resolved.controller_name).ShouldBeEmpty();
    }

    private static PrivacyContactGlobalContext BuildContext(ISettingProvider provider)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(provider);
        IServiceScopeFactory scopeFactory = services.BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        return new PrivacyContactGlobalContext(scopeFactory);
    }

    private static ISettingProvider StubProvider(Dictionary<string, string?> values)
    {
        ISettingProvider provider = Substitute.For<ISettingProvider>();
        provider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                string[] names = callInfo.Arg<string[]>();
                IReadOnlyList<SettingValue> result =
                [
                    .. names.Select(n => new SettingValue(
                        Name: n,
                        ProviderName: "G",
                        ProviderKey: null,
                        Value: values.TryGetValue(n, out string? v) ? v : null)),
                ];
                return Task.FromResult(result);
            });
        return provider;
    }
}
