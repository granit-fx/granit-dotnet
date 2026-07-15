using Granit.Identity.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests;

/// <summary>
/// Regression cover for the Vague 2b pepper reconciliation (G4): the lookup hasher and its
/// <c>Identity:LookupHasher:Pepper</c> are now owned by <see cref="GranitIdentityAbstractionsModule"/>
/// and shared by the local and federated stores. <see cref="GranitIdentityFederatedModule"/> adds a
/// startup guard that adopts an existing legacy <c>Identity:Federated:UserCacheHasher:EmailLookupPepper</c>
/// when the unified key is unset, and fails fast when both are set to different values — a silent
/// pepper divergence would break federated lookups against the persisted EmailHash digests.
/// </summary>
public sealed class LookupHasherPepperReconciliationTests
{
    private const string LegacyKey = "Identity:Federated:UserCacheHasher:EmailLookupPepper";
    private const string UnifiedKey = "Identity:LookupHasher:Pepper";

    private static UserLookupHasherOptions Resolve(params (string Key, string Value)[] config)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        foreach ((string key, string value) in config)
        {
            builder.Configuration[key] = value;
        }

        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
        // Abstractions binds Identity:LookupHasher and registers the hasher; Federated adds the
        // legacy-pepper reconciliation on top of the same options pipeline.
        new GranitIdentityAbstractionsModule().ConfigureServices(context);
        new GranitIdentityFederatedModule().ConfigureServices(context);

        ServiceProvider provider = builder.Services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<UserLookupHasherOptions>>().Value;
    }

    [Fact]
    public void AdoptsLegacyPepper_WhenUnifiedKeyUnset()
    {
        UserLookupHasherOptions options = Resolve((LegacyKey, "legacy-pepper-value"));

        // Existing federated-only deployments keep resolving their persisted EmailHash digests.
        options.Pepper.ShouldBe("legacy-pepper-value");
    }

    [Fact]
    public void UsesUnifiedPepper_WhenOnlyUnifiedKeySet()
    {
        UserLookupHasherOptions options = Resolve((UnifiedKey, "unified-pepper-value"));

        options.Pepper.ShouldBe("unified-pepper-value");
    }

    [Fact]
    public void Succeeds_WhenBothKeysSetToTheSameValue()
    {
        UserLookupHasherOptions options = Resolve(
            (LegacyKey, "same-pepper"),
            (UnifiedKey, "same-pepper"));

        options.Pepper.ShouldBe("same-pepper");
    }

    [Fact]
    public void Throws_WhenBothKeysSetToDifferentValues()
    {
        Action act = () => Resolve(
            (LegacyKey, "old-pepper"),
            (UnifiedKey, "new-pepper"));

        OptionsValidationException ex = act.ShouldThrow<OptionsValidationException>();
        ex.Message.ShouldContain(LegacyKey);
        ex.Message.ShouldContain(UnifiedKey);
    }

    [Fact]
    public void Throws_WhenNeitherKeyIsSet()
    {
        // Federated requires a pepper (encrypted PII lookups); a missing pepper must fail at startup.
        Action act = () => Resolve();

        act.ShouldThrow<OptionsValidationException>();
    }
}
