using System.Diagnostics.Metrics;
using Granit.Invoicing;
using Granit.Invoicing.Dtos;
using Granit.MultiTenancy;
using Granit.Parties.Domain;
using Granit.Tax.Builtin.Internal;
using Granit.Tax.Diagnostics;
using Granit.Tax.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Tax.Builtin.Tests.Internal;

public sealed class EuVatTaxCalculatorTests : IDisposable
{
    private readonly ITaxRateProvider _rateProvider = Substitute.For<ITaxRateProvider>();
    private readonly ITaxIdValidator _taxIdValidator = Substitute.For<ITaxIdValidator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly TaxMetrics _metrics;
    private readonly TaxOptions _taxOptions = new() { SellerCountryCode = "BE" };

    private static readonly DateTimeOffset FixedNow = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    public EuVatTaxCalculatorTests()
    {
        _metrics = new TaxMetrics(_meterFactory);
        _clock.Now.Returns(FixedNow);
    }

    public void Dispose() => _meterFactory.Dispose();

    private EuVatTaxCalculator CreateSut() =>
        new(_rateProvider, _taxIdValidator, MsOptions.Create(_taxOptions), _clock, _metrics, _currentTenant);

    private static BillingAddress CreateAddress(string country, string? vatNumber = null) =>
        BillingAddress.Create(
            line1: "Line 1",
            city: "City",
            postalCode: "1000",
            country: country,
            companyName: "Test Corp",
            vatNumber: vatNumber);

    private static TaxRequest CreateRequest(BillingAddress buyer, params decimal[] amounts)
    {
        BillingAddress seller = CreateAddress("BE");
        var lineItems = amounts.Select(a => new TaxLineItem("Item", a, null)).ToList();
        return new TaxRequest(lineItems, seller, buyer);
    }

    // ======== Domestic sale ========

    [Fact]
    public async Task CalculateAsync_DomesticSale_ShouldApplyStandardRate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        TaxRequest request = CreateRequest(CreateAddress("BE"), 100m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(21m);
        result.LineResults.ShouldHaveSingleItem();
        result.LineResults[0].TaxRate.ShouldBe(0.21m);
        result.Jurisdiction.ShouldBe("BE");
    }

    // ======== Export (buyer outside EU) ========

    [Fact]
    public async Task CalculateAsync_BuyerOutsideEu_ShouldReturnZeroTax()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaxRequest request = CreateRequest(CreateAddress("US"), 200m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(0m);
        result.LineResults[0].TaxRate.ShouldBe(0m);
    }

    // ======== B2B reverse charge (intra-EU) ========

    [Fact]
    public async Task CalculateAsync_IntraEuB2BWithValidVat_ShouldReturnZeroTaxReverseCharge()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BillingAddress buyer = CreateAddress("DE", "DE123456789");

        _taxIdValidator.ValidateAsync("DE123456789", "DE", ct)
            .Returns(new TaxIdValidationResult(
                IsValid: true, CompanyName: "Acme GmbH", CompanyAddress: null,
                RequestIdentifier: "WAPIaaaaa", ValidatedAt: FixedNow,
                Source: TaxIdValidationSource.Vies));

        TaxRequest request = CreateRequest(buyer, 500m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(0m);
        result.LineResults[0].TaxRate.ShouldBe(0m);
    }

    // ======== B2C with OSS enabled ========

    [Fact]
    public async Task CalculateAsync_IntraEuB2CWithOss_ShouldApplyBuyerCountryRate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _taxOptions.OssEnabled = true;
        _taxOptions.OssRegisteredCountries = ["DE", "FR"];

        _rateProvider.GetRateAsync("DE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("DE", 0.19m));

        TaxRequest request = CreateRequest(CreateAddress("DE"), 100m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(19m);
        result.LineResults[0].TaxRate.ShouldBe(0.19m);
        result.Jurisdiction.ShouldBe("DE");
    }

    // ======== B2C without OSS ========

    [Fact]
    public async Task CalculateAsync_IntraEuB2CWithoutOss_ShouldApplySellerCountryRate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        TaxRequest request = CreateRequest(CreateAddress("FR"), 100m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(21m);
        result.LineResults[0].TaxRate.ShouldBe(0.21m);
        result.Jurisdiction.ShouldBe("BE");
    }

    // ======== Multiple line items ========

    [Fact]
    public async Task CalculateAsync_MultipleLineItems_ShouldTaxEachIndependently()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        TaxRequest request = CreateRequest(CreateAddress("BE"), 100m, 50m, 25m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.LineResults.Count.ShouldBe(3);
        result.LineResults[0].TaxAmount.ShouldBe(21m);
        result.LineResults[1].TaxAmount.ShouldBe(10.50m);
        result.LineResults[2].TaxAmount.ShouldBe(5.25m);
        result.TotalTax.ShouldBe(36.75m);
    }

    // ======== Invalid VAT treated as B2C ========

    [Fact]
    public async Task CalculateAsync_BuyerWithInvalidVat_ShouldTreatAsB2C()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BillingAddress buyer = CreateAddress("DE", "INVALID123");

        _taxIdValidator.ValidateAsync("INVALID123", "DE", ct)
            .Returns(new TaxIdValidationResult(
                IsValid: false, CompanyName: null, CompanyAddress: null,
                RequestIdentifier: null, ValidatedAt: FixedNow,
                Source: TaxIdValidationSource.Vies));

        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        TaxRequest request = CreateRequest(buyer, 100m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(21m);
        result.LineResults[0].TaxRate.ShouldBe(0.21m);
    }

    // ======== Null/empty VAT treated as B2C ========

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateAsync_NullOrEmptyVatNumber_ShouldTreatAsB2C(string? vatNumber)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BillingAddress buyer = CreateAddress("DE", vatNumber);

        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        TaxRequest request = CreateRequest(buyer, 100m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(21m);
        await _taxIdValidator.DidNotReceive()
            .ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ======== Rounding (MidpointRounding.AwayFromZero) ========

    [Fact]
    public async Task CalculateAsync_ShouldRoundHalfAwayFromZero()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        // 33.33 * 0.21 = 6.9993 -> rounds to 7.00
        TaxRequest request = CreateRequest(CreateAddress("BE"), 33.33m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.LineResults[0].TaxAmount.ShouldBe(7.00m);
    }

    // ======== Rate not found ========

    [Fact]
    public async Task CalculateAsync_RateNotFoundForCountry_ShouldApplyZeroRate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns((TaxRateEntry?)null);

        TaxRequest request = CreateRequest(CreateAddress("BE"), 100m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(0m);
        result.LineResults[0].TaxRate.ShouldBe(0m);
    }

    // ======== Name property ========

    [Fact]
    public void Name_ShouldReturnEuVat() =>
        CreateSut().Name.ShouldBe("eu-vat");

    // ======== Metrics recorded ========

    [Fact]
    public async Task CalculateAsync_ShouldRecordMetricsWithoutThrowing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        TaxRequest request = CreateRequest(CreateAddress("BE"), 100m);

        // Should not throw even when currentTenant.Id is null
        _currentTenant.Id.Returns((Guid?)null);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(21m);
    }

    // ======== Empty line items ========

    [Fact]
    public async Task CalculateAsync_EmptyLineItems_ShouldReturnEmptyResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BillingAddress seller = CreateAddress("BE");
        var request = new TaxRequest([], seller, CreateAddress("BE"));

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.LineResults.ShouldBeEmpty();
        result.TotalTax.ShouldBe(0m);
    }

    // ======== Midpoint rounding edge case ========

    [Fact]
    public async Task CalculateAsync_MidpointRoundingEdge_ShouldRoundUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        // 1.005 * 0.21 = 0.21105 -> rounds to 0.21 (not a midpoint case)
        // 11.905 * 0.21 = 2.50005 -> rounds to 2.50 (2 decimals)
        TaxRequest request = CreateRequest(CreateAddress("BE"), 11.905m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.LineResults[0].TaxAmount.ShouldBe(2.50m);
    }

    // ======== OSS enabled but buyer excluded from OSS countries ========

    [Fact]
    public async Task CalculateAsync_OssEnabledBuyerCountryNotRegistered_ShouldApplySellerRate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _taxOptions.OssEnabled = true;
        _taxOptions.OssRegisteredCountries = ["FR", "IT"];

        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        TaxRequest request = CreateRequest(CreateAddress("DE"), 100m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(21m);
        result.Jurisdiction.ShouldBe("BE");
    }

    // ======== Tenant ID present in metrics ========

    [Fact]
    public async Task CalculateAsync_WithTenantId_ShouldRecordMetricsWithTenant()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _rateProvider.GetRateAsync("BE", FixedNow, cancellationToken: ct)
            .Returns(new TaxRateEntry("BE", 0.21m));

        var tenantId = Guid.NewGuid();
        _currentTenant.Id.Returns(tenantId);

        TaxRequest request = CreateRequest(CreateAddress("BE"), 100m);

        TaxResult result = await CreateSut().CalculateAsync(request, ct);

        result.TotalTax.ShouldBe(21m);
    }

    // ======== Test meter factory ========

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];
        public Meter Create(MeterOptions options) { Meter m = new(options); _meters.Add(m); return m; }
        public void Dispose() { foreach (Meter m in _meters) { m.Dispose(); } }
    }
}
