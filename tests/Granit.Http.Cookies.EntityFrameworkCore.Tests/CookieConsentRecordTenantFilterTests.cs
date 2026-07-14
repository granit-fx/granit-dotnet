// =============================================================================
// CookieConsentRecordTenantFilterTests — multi-tenant isolation of the ledger
// =============================================================================
// CookieConsentRecord is IMultiTenant, so the parameterised named filter installed
// by GranitDbContext applies to it. SQLite (never InMemory) because the InMemory
// provider does not faithfully execute query filters (CLAUDE.md).
// =============================================================================

using Granit.Http.Cookies.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test the internal DbContext and services

namespace Granit.Http.Cookies.EntityFrameworkCore.Tests;

public sealed class CookieConsentRecordTenantFilterTests : IAsyncDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CookiesDbContext> _dbOptions;

    public CookieConsentRecordTenantFilterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<CookiesDbContext>()
            .UseSqlite(_connection)
            .Options;

        using CookiesDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        context.Database.EnsureCreated();

        // Seed one decision per partition (tenant A, tenant B, host).
        context.ConsentRecords.Add(CreateRecord(_tenantA, "cmp-a"));
        context.ConsentRecords.Add(CreateRecord(_tenantB, "cmp-b"));
        context.ConsentRecords.Add(CreateRecord(tenantId: null, "cmp-host"));
        context.SaveChanges();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task ConsentRecords_TenantBoundContext_SeesOnlyItsOwnRows()
    {
        // Arrange
        await using CookiesDbContext context = new(_dbOptions, TenantContext(_tenantA));

        // Act
        List<CookieConsentRecord> rows = await context.ConsentRecords.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        CookieConsentRecord row = rows.ShouldHaveSingleItem();
        row.TenantId.ShouldBe(_tenantA);
        row.CmpSource.ShouldBe("cmp-a");
    }

    [Fact]
    public async Task ConsentRecords_NoTenantContext_FailsClosedToHostPartition()
    {
        // Arrange — absent tenant context: the filter stays active with
        // CurrentTenantId null, so only host rows are visible — never tenant data.
        await using CookiesDbContext context = new(_dbOptions, TenantContext(tenantId: null));

        // Act
        List<CookieConsentRecord> rows = await context.ConsentRecords.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        rows.ShouldHaveSingleItem().TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task QueryableSource_WithSignaledHostAccess_ReadsCrossTenant()
    {
        // Arrange — the real TenantQueryScope from AddGranitPersistence(), with the
        // explicit host-access signal: consent statistics are reviewable cross-tenant.
        ICurrentTenant currentTenant = TenantContext(tenantId: null);
        IHostAccessContext hostAccess = Substitute.For<IHostAccessContext>();
        hostAccess.IsHostAccess.Returns(true);
        await using ServiceProvider provider = BuildProvider(currentTenant, hostAccess);
        using IServiceScope scope = provider.CreateScope();
        await using EfCookieConsentRecordQueryableSource source = new(
            new StubCookiesDbContextFactory(_dbOptions, currentTenant),
            scope.ServiceProvider.GetRequiredService<ITenantQueryScope>());

        // Act
        List<CookieConsentRecord> rows = await source.GetQueryable()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert — all three partitions visible.
        rows.Count.ShouldBe(3);
        rows.ShouldContain(r => r.TenantId == _tenantA);
        rows.ShouldContain(r => r.TenantId == _tenantB);
        rows.ShouldContain(r => r.TenantId == null);
    }

    [Fact]
    public async Task QueryableSource_TenantBound_RestrictsToTenantRows()
    {
        // Arrange
        ICurrentTenant currentTenant = TenantContext(_tenantB);
        await using ServiceProvider provider = BuildProvider(currentTenant);
        using IServiceScope scope = provider.CreateScope();
        await using EfCookieConsentRecordQueryableSource source = new(
            new StubCookiesDbContextFactory(_dbOptions, currentTenant),
            scope.ServiceProvider.GetRequiredService<ITenantQueryScope>());

        // Act
        List<CookieConsentRecord> rows = await source.GetQueryable()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        rows.ShouldHaveSingleItem().TenantId.ShouldBe(_tenantB);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ICurrentTenant TenantContext(Guid? tenantId)
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(tenantId is not null);
        currentTenant.Id.Returns(tenantId);
        return currentTenant;
    }

    private static ServiceProvider BuildProvider(
        ICurrentTenant currentTenant,
        IHostAccessContext? hostAccess = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(currentTenant);
        if (hostAccess is not null)
        {
            services.AddSingleton(hostAccess);
        }

        services.AddGranitPersistence();
        return services.BuildServiceProvider();
    }

    internal static CookieConsentRecord CreateRecord(Guid? tenantId, string cmpSource)
    {
        var record = CookieConsentRecord.Create(
            ["strictly_necessary"],
            ["marketing"],
            CookieConsentMode.OptIn,
            cmpSource,
            new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero));
        ((IMultiTenant)record).TenantId = tenantId;
        return record;
    }
}
