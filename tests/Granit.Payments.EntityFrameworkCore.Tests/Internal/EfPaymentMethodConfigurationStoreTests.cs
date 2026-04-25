using System.Collections.Immutable;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Payments.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Payments.EntityFrameworkCore.Tests.Internal;

[Collection(PaymentsDbSerialGroup.Name)]
public sealed class EfPaymentMethodConfigurationStoreTests : IAsyncDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly DataFilter _dataFilter = new();
    private readonly EfPaymentMethodConfigurationStore _store;

    public EfPaymentMethodConfigurationStoreTests()
    {
        DbContextOptions<PaymentsDbContext> options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase($"payments-cfg-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestDbContextFactory(options, _dataFilter);
        _store = new EfPaymentMethodConfigurationStore(_factory, _dataFilter);
    }

    public async ValueTask DisposeAsync()
    {
        await using PaymentsDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private static PaymentMethodCapability MakeCapability() =>
        new(
            SupportedCountries: ImmutableHashSet.Create("BE", "FR"),
            SupportedCurrencies: ImmutableHashSet.Create("EUR"),
            SupportedSequenceTypes: PaymentMethodSequenceType.OneOff,
            AmountBounds: ImmutableDictionary<string, PaymentMethodAmountBound>.Empty);

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTrips()
    {
        var cfg = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        await _store.AddAsync(cfg, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? loaded = await _store.GetByIdAsync(
            cfg.Id, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.ProviderName.ShouldBe("stripe");
        loaded.MethodType.ShouldBe("card");
        loaded.Activated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllIncludingInactive()
    {
        var active = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        var inactive = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "mollie", "ideal");
        inactive.Deactivate();

        await _store.AddAsync(active, TestContext.Current.CancellationToken);
        await _store.AddAsync(inactive, TestContext.Current.CancellationToken);

        IReadOnlyList<PaymentMethodConfiguration> result = await _store.GetAllAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.Select(c => c.Activated).ShouldContain(false);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsAllActiveRows()
    {
        var active1 = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        var active2 = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "mollie", "ideal");
        await _store.AddAsync(active1, TestContext.Current.CancellationToken);
        await _store.AddAsync(active2, TestContext.Current.CancellationToken);

        IReadOnlyList<PaymentMethodConfiguration> result = await _store.GetActiveAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Select(c => c.ProviderName).ShouldContain("stripe");
        result.Select(c => c.ProviderName).ShouldContain("mollie");
    }

    [Fact]
    public async Task FindAsync_MatchesProviderAndMethod()
    {
        var cfg = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        await _store.AddAsync(cfg, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? hit = await _store.FindAsync(
            "stripe", "card", TestContext.Current.CancellationToken);
        PaymentMethodConfiguration? miss = await _store.FindAsync(
            "stripe", "missing", TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        miss.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        PaymentMethodConfiguration? result = await _store.GetByIdAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var cfg = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        await _store.AddAsync(cfg, TestContext.Current.CancellationToken);

        cfg.Deactivate();
        await _store.UpdateAsync(cfg, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? loaded = await _store.GetByIdAsync(
            cfg.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Activated.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesRow()
    {
        var cfg = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        await _store.AddAsync(cfg, TestContext.Current.CancellationToken);

        await _store.DeleteAsync(cfg, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? loaded = await _store.GetByIdAsync(
            cfg.Id, TestContext.Current.CancellationToken);
        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task UpsertActivationAsync_NewRecord_Creates()
    {
        await _store.UpsertActivationAsync(
            Guid.NewGuid(), "stripe", "card", isActive: true, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? loaded = await _store.FindAsync(
            "stripe", "card", TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Activated.ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertActivationAsync_NewRecordInactive_CreatesDeactivated()
    {
        await _store.UpsertActivationAsync(
            Guid.NewGuid(), "stripe", "card", isActive: false, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? loaded = await _store.FindAsync(
            "stripe", "card", TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Activated.ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertActivationAsync_ExistingRecord_TogglesState()
    {
        var cfg = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        cfg.Deactivate();
        await _store.AddAsync(cfg, TestContext.Current.CancellationToken);

        await _store.UpsertActivationAsync(
            Guid.NewGuid(), "stripe", "card", isActive: true, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? loaded = await _store.GetByIdAsync(
            cfg.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Activated.ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertActivationWithSnapshotAsync_NewRecord_StoresCapability()
    {
        PaymentMethodCapability cap = MakeCapability();

        await _store.UpsertActivationWithSnapshotAsync(
            Guid.NewGuid(), "stripe", "card", cap, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? loaded = await _store.FindAsync(
            "stripe", "card", TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Activated.ShouldBeTrue();
        loaded.SupportedCountries.ShouldNotBeNull();
        loaded.SupportedCountries.ShouldContain("BE");
    }

    [Fact]
    public async Task UpsertActivationWithSnapshotAsync_ExistingRecord_RefreshesSnapshot()
    {
        var cfg = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        await _store.AddAsync(cfg, TestContext.Current.CancellationToken);

        PaymentMethodCapability cap = MakeCapability();
        await _store.UpsertActivationWithSnapshotAsync(
            Guid.NewGuid(), "stripe", "card", cap, TestContext.Current.CancellationToken);

        PaymentMethodConfiguration? loaded = await _store.GetByIdAsync(
            cfg.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.SupportedCountries.ShouldNotBeNull();
        loaded.SupportedCountries.ShouldContain("FR");
    }

    [Fact]
    public async Task UpdateCapabilitySnapshotAsync_Existing_ReturnsTrue()
    {
        var cfg = PaymentMethodConfiguration.Activate(Guid.NewGuid(), "stripe", "card");
        await _store.AddAsync(cfg, TestContext.Current.CancellationToken);

        bool updated = await _store.UpdateCapabilitySnapshotAsync(
            "stripe", "card", MakeCapability(), TestContext.Current.CancellationToken);

        updated.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateCapabilitySnapshotAsync_Missing_ReturnsFalse()
    {
        bool updated = await _store.UpdateCapabilitySnapshotAsync(
            "stripe", "card", MakeCapability(), TestContext.Current.CancellationToken);

        updated.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateCapabilitySnapshotAsync_NullCapability_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _store.UpdateCapabilitySnapshotAsync(
                "stripe", "card", null!, TestContext.Current.CancellationToken));

    [Fact]
    public async Task UpsertActivationWithSnapshotAsync_NullCapability_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _store.UpsertActivationWithSnapshotAsync(
                Guid.NewGuid(), "stripe", "card", null!, TestContext.Current.CancellationToken));

    private sealed class TestDbContextFactory(
        DbContextOptions<PaymentsDbContext> options, IDataFilter filter)
        : IDbContextFactory<PaymentsDbContext>
    {
        private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();

        public PaymentsDbContext CreateDbContext() => new(options, _tenant, filter);

        public Task<PaymentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentsDbContext(options, _tenant, filter));
    }
}

[CollectionDefinition(PaymentsDbSerialGroup.Name, DisableParallelization = true)]
public sealed class PaymentsDbSerialGroup
{
    public const string Name = "Payments-Db-serial";
}
