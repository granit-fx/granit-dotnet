using Granit.MultiTenancy;
using Granit.Privacy.Regulations.Internal;
using Granit.Privacy.Regulations.Options;
using Granit.Privacy.Regulations.Profiles;
using Granit.Privacy.Regulations.Profiles.Internal;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Tests.Internal;

public sealed class TenantBasedRegulationResolverTests
{
    private readonly IRegulationProfileRegistry _registry;

    public TenantBasedRegulationResolverTests()
    {
        RegulationProfileContext context = new();
        new BuiltInRegulationProfileProvider().Define(context);
        _registry = new RegulationProfileRegistry(context.Build());
    }

    [Fact]
    public async Task ResolveAsync_DefaultRegulation_ReturnsProfile()
    {
        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "EU_GDPR" });

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        profile.Regulation.Value.ShouldBe("EU_GDPR");
    }

    [Fact]
    public async Task ResolveAsync_PerTenantOverride_TakesPrecedence()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId);

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions
            {
                DefaultRegulation = "EU_GDPR",
                TenantRegulations = new Dictionary<string, string>
                {
                    [tenantId.ToString()] = "BR_LGPD",
                },
            },
            tenant);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        profile.Regulation.Value.ShouldBe("BR_LGPD");
    }

    [Fact]
    public async Task ResolveAsync_NoTenantOverride_FallsBackToDefault()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId);

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "US_CCPA" },
            tenant);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        profile.Regulation.Value.ShouldBe("US_CCPA");
    }

    [Fact]
    public async Task ResolveAsync_NoConfiguration_Throws()
    {
        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions());

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("No privacy regulation configured");
    }

    [Fact]
    public async Task ResolveAsync_UnknownRegulation_Throws()
    {
        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "XX_UNKNOWN" });

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("not registered");
        ex.Message.ShouldContain("Available regulations");
    }

    [Fact]
    public async Task ResolveAsync_NullCurrentTenant_UsesDefault()
    {
        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "CH_NFADP" },
            currentTenant: null);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        profile.Regulation.Value.ShouldBe("CH_NFADP");
    }

    [Fact]
    public async Task ResolveAllAsync_ReturnsSingleProfile()
    {
        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "EU_GDPR" });

        IReadOnlyList<PrivacyRegulationProfile> profiles = await resolver.ResolveAllAsync(TestContext.Current.CancellationToken);

        profiles.Count.ShouldBe(1);
        profiles[0].Regulation.Value.ShouldBe("EU_GDPR");
    }

    private TenantBasedRegulationResolver CreateResolver(
        PrivacyRegulationsOptions options,
        ICurrentTenant? currentTenant = null)
    {
        IOptions<PrivacyRegulationsOptions> opts = Microsoft.Extensions.Options.Options.Create(options);
        return new TenantBasedRegulationResolver(_registry, opts, currentTenant);
    }

    private static ICurrentTenant CreateTenant(Guid tenantId)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(tenantId);
        return tenant;
    }
}
