using Granit.DataFiltering;
using Granit.Parties;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Tax;
using Granit.Tax.Domain;
using Granit.Tax.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Tax.EntityFrameworkCore.Tests.Internal;

[Collection(TaxDbSerialGroup.Name)]
public sealed class EfTaxRateProviderTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TestFactory _factory;
    private readonly ITaxRateProvider _fallback = Substitute.For<ITaxRateProvider>();
    private readonly IPartyReader _partyReader = Substitute.For<IPartyReader>();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly EfTaxRateProvider _sut;

    public EfTaxRateProviderTests()
    {
        DbContextOptions<TaxDbContext> options = new DbContextOptionsBuilder<TaxDbContext>()
            .UseInMemoryDatabase($"tax-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestFactory(options);
        _clock.Now.Returns(Now);
        _dataFilter.Disable<Granit.Domain.IMultiTenant>().Returns(new NullDisposable());
        _sut = new EfTaxRateProvider(_factory, _fallback, _partyReader, _dataFilter, _clock);
    }

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose() { }
    }

    public async ValueTask DisposeAsync()
    {
        await using TaxDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private async Task SeedOverrideAsync(
        string country, decimal standard, decimal? reduced = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        await using TaxDbContext db = await _factory.CreateDbContextAsync();
        db.TaxRateOverrides.Add(TaxRateOverride.Create(
            Guid.NewGuid(), country, standard,
            effectiveFrom: from ?? Now.AddDays(-30),
            reducedRate: reduced,
            effectiveTo: to));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRateAsync_DbOverrideExists_ReturnsOverride()
    {
        await SeedOverrideAsync("BE", 0.21m, reduced: 0.06m);

        TaxRateEntry? result = await _sut.GetRateAsync("BE", Now, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.CountryCode.ShouldBe("BE");
        result.StandardRate.ShouldBe(0.21m);
        result.ReducedRate.ShouldBe(0.06m);
        await _fallback.DidNotReceive().GetRateAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRateAsync_NoOverride_DelegatesToFallback()
    {
        TaxRateEntry fallbackEntry = new("FR", 0.20m, ReducedRate: 0.055m, EffectiveFrom: Now.AddYears(-1));
        _fallback.GetRateAsync("FR", Now, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(fallbackEntry);

        TaxRateEntry? result = await _sut.GetRateAsync("FR", Now, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(fallbackEntry);
    }

    [Fact]
    public async Task GetRateAsync_OverrideOutsideDate_DelegatesToFallback()
    {
        await SeedOverrideAsync("BE", 0.99m,
            from: Now.AddDays(-365),
            to: Now.AddDays(-7)); // expired

        _fallback.GetRateAsync("BE", Arg.Any<DateTimeOffset>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new TaxRateEntry("BE", 0.21m, EffectiveFrom: Now.AddYears(-2)));

        TaxRateEntry? result = await _sut.GetRateAsync("BE", Now, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.StandardRate.ShouldBe(0.21m); // fallback
    }

    [Fact]
    public async Task GetRateAsync_MultipleOverrides_PicksMostRecentEffectiveFrom()
    {
        await SeedOverrideAsync("BE", 0.20m, from: Now.AddDays(-90));
        await SeedOverrideAsync("BE", 0.21m, from: Now.AddDays(-30));

        TaxRateEntry? result = await _sut.GetRateAsync("BE", Now, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.StandardRate.ShouldBe(0.21m);
    }

    [Fact]
    public async Task GetAllCurrentRatesAsync_OverrideMergesIntoFallbackList()
    {
        TaxRateEntry[] fallback = [
            new("BE", 0.21m, EffectiveFrom: Now.AddYears(-1)),
            new("FR", 0.20m, EffectiveFrom: Now.AddYears(-1)),
        ];
        _fallback.GetAllCurrentRatesAsync(Arg.Any<CancellationToken>())
            .Returns(fallback);

        await SeedOverrideAsync("BE", 0.99m, reduced: 0.06m);

        IReadOnlyList<TaxRateEntry> result = await _sut.GetAllCurrentRatesAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        TaxRateEntry be = result.First(r => r.CountryCode == "BE");
        be.StandardRate.ShouldBe(0.99m);
        be.ReducedRate.ShouldBe(0.06m);
        TaxRateEntry fr = result.First(r => r.CountryCode == "FR");
        fr.StandardRate.ShouldBe(0.20m); // unchanged
    }

    [Fact]
    public async Task GetAllCurrentRates_Sync_OverrideMergesIntoFallbackList()
    {
        TaxRateEntry[] fallback = [
            new("BE", 0.21m, EffectiveFrom: Now.AddYears(-1)),
        ];
        _fallback.GetAllCurrentRates().Returns(fallback);

        await SeedOverrideAsync("BE", 0.99m);

        IReadOnlyList<TaxRateEntry> result = _sut.GetAllCurrentRates();

        result.Count.ShouldBe(1);
        result[0].StandardRate.ShouldBe(0.99m);
    }

    // ── Customer-specific tax status (US #1244) ────────────────────────

    [Fact]
    public async Task GetRateAsync_ContactExempt_ReturnsZeroRate_BypassingDbAndFallback()
    {
        await SeedOverrideAsync("BE", 0.21m); // would normally apply
        var partyId = PartyId.Create(Guid.NewGuid());
        Party party = NewPartyWithStatus(TaxStatus.Create(isExempt: true));
        _partyReader.GetByIdAsync(partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        TaxRateEntry? result = await _sut.GetRateAsync(
            "BE", Now, partyId: partyId, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.StandardRate.ShouldBe(0m);
        result.ReducedRate.ShouldBe(0m);
        await _fallback.DidNotReceive().GetRateAsync(
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<PartyId?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRateAsync_ContactReverseCharge_ReturnsZeroRate()
    {
        var partyId = PartyId.Create(Guid.NewGuid());
        Party party = NewPartyWithStatus(TaxStatus.Create(reverseCharge: true, vatin: "BE0123456789"));
        _partyReader.GetByIdAsync(partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        TaxRateEntry? result = await _sut.GetRateAsync(
            "FR", Now, partyId: partyId, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.StandardRate.ShouldBe(0m);
        result.ReducedRate.ShouldBe(0m);
    }

    [Fact]
    public async Task GetRateAsync_ContactStandard_DelegatesToFallback()
    {
        var partyId = PartyId.Create(Guid.NewGuid());
        Party party = NewPartyWithStatus(TaxStatus.Standard);
        _partyReader.GetByIdAsync(partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        TaxRateEntry fallbackEntry = new("FR", 0.20m, EffectiveFrom: Now.AddYears(-1));
        _fallback.GetRateAsync(
                "FR", Now, partyId, Arg.Any<CancellationToken>())
            .Returns(fallbackEntry);

        TaxRateEntry? result = await _sut.GetRateAsync(
            "FR", Now, partyId: partyId, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(fallbackEntry);
    }

    [Fact]
    public async Task GetRateAsync_ContactNotFound_FallsThroughToCountryRate()
    {
        var partyId = PartyId.Create(Guid.NewGuid());
        _partyReader.GetByIdAsync(partyId, Arg.Any<CancellationToken>())
            .Returns((Party?)null);
        await SeedOverrideAsync("BE", 0.21m);

        TaxRateEntry? result = await _sut.GetRateAsync(
            "BE", Now, partyId: partyId, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.StandardRate.ShouldBe(0.21m);
    }

    private static Party NewPartyWithStatus(TaxStatus status)
    {
        var c = Party.Create(Guid.NewGuid(), tenantId: null, PartyKind.Company, "Test", "EUR");
        c.SetTaxStatus(status);
        return c;
    }

    private sealed class TestFactory(DbContextOptions<TaxDbContext> options)
        : IDbContextFactory<TaxDbContext>
    {
        public TaxDbContext CreateDbContext() => new(options);

        public Task<TaxDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TaxDbContext(options));
    }
}

[CollectionDefinition(TaxDbSerialGroup.Name, DisableParallelization = true)]
public sealed class TaxDbSerialGroup
{
    public const string Name = "Tax-Db-serial";
}
