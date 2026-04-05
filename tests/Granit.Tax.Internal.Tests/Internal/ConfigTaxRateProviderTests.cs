using Granit.Tax.Internal.Internal;
using Granit.Tax.Internal.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Tax.Internal.Tests.Internal;

public sealed class ConfigTaxRateProviderTests
{
    private static ConfigTaxRateProvider CreateProvider(EuVatRateOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? new EuVatRateOptions()));

    // ======== GetRateAsync ========

    [Fact]
    public async Task GetRateAsync_DefaultCountry_ShouldReturnDefaultRate()
    {
        ConfigTaxRateProvider provider = CreateProvider();

        TaxRateEntry? entry = await provider.GetRateAsync("BE", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        entry.ShouldNotBeNull();
        entry.CountryCode.ShouldBe("BE");
        entry.StandardRate.ShouldBe(0.21m);
        entry.ReducedRate.ShouldBeNull();
    }

    [Fact]
    public async Task GetRateAsync_ConfiguredOverride_ShouldReturnOverrideRate()
    {
        var options = new EuVatRateOptions
        {
            StandardRates = new Dictionary<string, decimal> { ["BE"] = 0.22m },
        };
        ConfigTaxRateProvider provider = CreateProvider(options);

        TaxRateEntry? entry = await provider.GetRateAsync("BE", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        entry.ShouldNotBeNull();
        entry.StandardRate.ShouldBe(0.22m);
    }

    [Fact]
    public async Task GetRateAsync_ConfiguredWithReducedRate_ShouldReturnReducedRate()
    {
        var options = new EuVatRateOptions
        {
            StandardRates = new Dictionary<string, decimal> { ["FR"] = 0.20m },
            ReducedRates = new Dictionary<string, decimal> { ["FR"] = 0.055m },
        };
        ConfigTaxRateProvider provider = CreateProvider(options);

        TaxRateEntry? entry = await provider.GetRateAsync("FR", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        entry.ShouldNotBeNull();
        entry.StandardRate.ShouldBe(0.20m);
        entry.ReducedRate.ShouldBe(0.055m);
    }

    [Fact]
    public async Task GetRateAsync_UnknownCountry_ShouldReturnNull()
    {
        ConfigTaxRateProvider provider = CreateProvider();

        TaxRateEntry? entry = await provider.GetRateAsync("ZZ", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        entry.ShouldBeNull();
    }

    [Fact]
    public async Task GetRateAsync_LowercaseInput_ShouldNormalizeAndReturn()
    {
        ConfigTaxRateProvider provider = CreateProvider();

        TaxRateEntry? entry = await provider.GetRateAsync("de", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        entry.ShouldNotBeNull();
        entry.CountryCode.ShouldBe("DE");
        entry.StandardRate.ShouldBe(0.19m);
    }

    // ======== GetAllCurrentRatesAsync ========

    [Fact]
    public async Task GetAllCurrentRatesAsync_ShouldReturnAllEuCountries()
    {
        ConfigTaxRateProvider provider = CreateProvider();

        IReadOnlyList<TaxRateEntry> rates = await provider.GetAllCurrentRatesAsync(TestContext.Current.CancellationToken);

        rates.Count.ShouldBe(27);
    }

    [Fact]
    public async Task GetAllCurrentRatesAsync_WithOverride_ShouldUseOverrideRate()
    {
        var options = new EuVatRateOptions
        {
            StandardRates = new Dictionary<string, decimal> { ["LU"] = 0.18m },
            ReducedRates = new Dictionary<string, decimal> { ["LU"] = 0.08m },
        };
        ConfigTaxRateProvider provider = CreateProvider(options);

        IReadOnlyList<TaxRateEntry> rates = await provider.GetAllCurrentRatesAsync(TestContext.Current.CancellationToken);

        TaxRateEntry lu = rates.Single(r => r.CountryCode == "LU");
        lu.StandardRate.ShouldBe(0.18m);
        lu.ReducedRate.ShouldBe(0.08m);
    }
}
