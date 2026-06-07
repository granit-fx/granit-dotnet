using Granit.MultiTenancy;
using Granit.Privacy.Regulations.Internal;
using Granit.Privacy.Regulations.Jurisdiction;
using Granit.Privacy.Regulations.Jurisdiction.Internal;
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
    private readonly DefaultPrivacyJurisdictionResolver _jurisdictionResolver =
        new([new BuiltInPrivacyJurisdictionMapProvider()]);

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
    public async Task ResolveAsync_PerTenantConfigOverride_TakesPrecedence()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, null);

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
    public async Task ResolveAsync_IsoJurisdiction_ResolvesToMatchingRegulation()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, "BR");

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "EU_GDPR" },
            tenant);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        // "BR" → BR_LGPD via IPrivacyJurisdictionResolver
        profile.Regulation.Value.ShouldBe("BR_LGPD");
    }

    [Fact]
    public async Task ResolveAsync_ConfigOverrideTakesPrecedenceOverIsoJurisdiction()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, "BR");

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions
            {
                DefaultRegulation = "EU_GDPR",
                TenantRegulations = new Dictionary<string, string>
                {
                    [tenantId.ToString()] = "US_CCPA",
                },
            },
            tenant);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        profile.Regulation.Value.ShouldBe("US_CCPA");
    }

    [Fact]
    public async Task ResolveAsync_NullJurisdiction_FallsBackToConfigDefault()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, null);

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "CH_NFADP" },
            tenant);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        profile.Regulation.Value.ShouldBe("CH_NFADP");
    }

    [Fact]
    public async Task ResolveAsync_IsoCodeResolvesToNothing_FallsBackToDefault()
    {
        // "US" has no federal general privacy law → resolver returns [] → fallback to default
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, "US");

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "EU_GDPR" },
            tenant);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        profile.Regulation.Value.ShouldBe("EU_GDPR");
    }

    [Fact]
    public async Task ResolveAsync_RegionCode_TakesPrecedenceOverCountry()
    {
        // "CA-QC" → CA_QUEBEC_25 + CA_PIPEDA (region match)
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, "CA-QC");

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "EU_GDPR" },
            tenant);

        IReadOnlyList<PrivacyRegulationProfile> profiles =
            await resolver.ResolveAllAsync(TestContext.Current.CancellationToken);

        profiles.Count.ShouldBe(2);
        profiles.Select(p => p.Regulation.Value).ShouldContain("CA_QUEBEC_25");
        profiles.Select(p => p.Regulation.Value).ShouldContain("CA_PIPEDA");
    }

    [Fact]
    public async Task ResolveAsync_SwitzerlandIsoCode_MergesIntoCompositeProfile()
    {
        // "CH" → CH_NFADP + EU_GDPR; ResolveAsync merges them into a single composite
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, "CH");

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "EU_GDPR" },
            tenant);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        // Merged profile covers both CH_NFADP and EU_GDPR
        profile.ConsentModel.ShouldBe(ConsentModel.OptIn);
    }

    [Fact]
    public async Task ResolveAllAsync_FranceIsoCode_ReturnsSingleProfile()
    {
        // "FR" → EU_GDPR only; ResolveAllAsync returns a single-element list
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, "FR");

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "CH_NFADP" },
            tenant);

        IReadOnlyList<PrivacyRegulationProfile> profiles =
            await resolver.ResolveAllAsync(TestContext.Current.CancellationToken);

        profiles.Count.ShouldBe(1);
        profiles[0].Regulation.Value.ShouldBe("EU_GDPR");
    }

    [Fact]
    public async Task ResolveAsync_SwitzerlandIsoCode_ReturnsDualRegulation()
    {
        // "CH" → CH_NFADP + EU_GDPR (for EU data subjects)
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = CreateTenant(tenantId, "CH");

        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "EU_GDPR" },
            tenant);

        IReadOnlyList<PrivacyRegulationProfile> profiles =
            await resolver.ResolveAllAsync(TestContext.Current.CancellationToken);

        profiles.Count.ShouldBe(2);
        profiles.Select(p => p.Regulation.Value).ShouldContain("CH_NFADP");
        profiles.Select(p => p.Regulation.Value).ShouldContain("EU_GDPR");
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
    public async Task ResolveAllAsync_SingleJurisdiction_ReturnsSingleProfile()
    {
        TenantBasedRegulationResolver resolver = CreateResolver(
            new PrivacyRegulationsOptions { DefaultRegulation = "EU_GDPR" });

        IReadOnlyList<PrivacyRegulationProfile> profiles = await resolver.ResolveAllAsync(TestContext.Current.CancellationToken);

        profiles.Count.ShouldBe(1);
        profiles[0].Regulation.Value.ShouldBe("EU_GDPR");
    }

    private TenantBasedRegulationResolver CreateResolver(
        PrivacyRegulationsOptions options,
        ICurrentTenant? currentTenant = null,
        IPrivacyJurisdictionResolver? jurisdictionResolver = null)
    {
        IOptions<PrivacyRegulationsOptions> opts = Microsoft.Extensions.Options.Options.Create(options);
        return new TenantBasedRegulationResolver(_registry, opts, jurisdictionResolver ?? _jurisdictionResolver, currentTenant);
    }

    private static ICurrentTenant CreateTenant(Guid tenantId, string? jurisdiction)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(tenantId);
        tenant.Jurisdiction.Returns(jurisdiction);
        return tenant;
    }
}
