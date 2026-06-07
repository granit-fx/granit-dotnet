using Granit.Privacy.Regulations.Jurisdiction.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Tests.Jurisdiction;

public sealed class DefaultPrivacyJurisdictionResolverTests
{
    private readonly DefaultPrivacyJurisdictionResolver _resolver =
        new DefaultPrivacyJurisdictionResolver([new BuiltInPrivacyJurisdictionMapProvider()]);

    // ── EU member states ─────────────────────────────────────────────────

    [Theory]
    [InlineData("FR")]
    [InlineData("DE")]
    [InlineData("BE")]
    [InlineData("NL")]
    [InlineData("IT")]
    [InlineData("ES")]
    [InlineData("PL")]
    public async Task Resolve_EuMemberState_ReturnsEuGdpr(string countryCode)
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync(countryCode, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Value.ShouldBe("EU_GDPR");
    }

    // ── Tier 1 country mappings ──────────────────────────────────────────

    [Fact]
    public async Task Resolve_UnitedKingdom_ReturnsUkGdpr()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("GB", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Value.ShouldBe("UK_GDPR");
    }

    [Fact]
    public async Task Resolve_Brazil_ReturnsLgpd()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("BR", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Value.ShouldBe("BR_LGPD");
    }

    [Fact]
    public async Task Resolve_Switzerland_ReturnsNfadpAndEuGdpr()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("CH", cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.Select(r => r.Value).ShouldBe(["CH_NFADP", "EU_GDPR"], ignoreOrder: false);
    }

    [Fact]
    public async Task Resolve_Canada_CountryLevel_ReturnsPipeda()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("CA", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Value.ShouldBe("CA_PIPEDA");
    }

    // ── Region overrides (Canada) ────────────────────────────────────────

    [Fact]
    public async Task Resolve_QuebecRegion_ReturnsQuebec25AndPipeda()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("CA", "CA-QC", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.Select(r => r.Value).ShouldContain("CA_QUEBEC_25");
        result.Select(r => r.Value).ShouldContain("CA_PIPEDA");
    }

    [Fact]
    public async Task Resolve_QuebecRegion_TakesPrecedenceOverCountry()
    {
        IReadOnlyList<PrivacyRegulation> withRegion =
            await _resolver.ResolveAsync("CA", "CA-QC", TestContext.Current.CancellationToken);

        IReadOnlyList<PrivacyRegulation> withoutRegion =
            await _resolver.ResolveAsync("CA", cancellationToken: TestContext.Current.CancellationToken);

        withRegion.Count.ShouldBeGreaterThan(withoutRegion.Count);
    }

    // ── US ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_UsCountryLevel_ReturnsEmpty()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("US", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_CaliforniaRegion_ReturnsCcpa()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("US", "US-CA", TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Value.ShouldBe("US_CCPA");
    }

    // ── Tier 2 ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CN", "CN_PIPL")]
    [InlineData("IN", "IN_DPDPA")]
    [InlineData("JP", "JP_APPI")]
    [InlineData("KR", "KR_PIPA")]
    [InlineData("AU", "AU_PRIVACY_ACT")]
    [InlineData("ZA", "ZA_POPIA")]
    [InlineData("TH", "TH_PDPA")]
    public async Task Resolve_Tier2Country_ReturnsExpectedRegulation(string countryCode, string expectedCode)
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync(countryCode, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Value.ShouldBe(expectedCode);
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_UnknownCountry_ReturnsEmpty()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("XX", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_UnknownRegion_FallsBackToCountry()
    {
        IReadOnlyList<PrivacyRegulation> result =
            await _resolver.ResolveAsync("FR", "FR-XX", TestContext.Current.CancellationToken);

        // No FR-XX region → falls back to country FR → EU_GDPR
        result.ShouldHaveSingleItem().Value.ShouldBe("EU_GDPR");
    }

    [Fact]
    public async Task Resolve_LookupIsCaseInsensitive()
    {
        IReadOnlyList<PrivacyRegulation> upper =
            await _resolver.ResolveAsync("FR", cancellationToken: TestContext.Current.CancellationToken);

        IReadOnlyList<PrivacyRegulation> lower =
            await _resolver.ResolveAsync("fr", cancellationToken: TestContext.Current.CancellationToken);

        upper.Count.ShouldBe(lower.Count);
        upper[0].Value.ShouldBe(lower[0].Value);
    }
}
