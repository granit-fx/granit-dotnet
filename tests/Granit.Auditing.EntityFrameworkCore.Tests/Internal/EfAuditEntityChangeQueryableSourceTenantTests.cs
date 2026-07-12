// =============================================================================
// EfAuditEntityChangeQueryableSourceTenantTests - cross-tenant non-regression
// =============================================================================
// AuditEntityChange is IMultiTenant with the tenant id denormalized from the
// parent AuditEntry by AuditingBatchMapper, so the parameterised multi-tenant
// named filter installed by GranitDbContext applies to it DIRECTLY (no parent
// join needed). Verifies, with the REAL TenantQueryScope resolved through
// AddGranitPersistence():
//   - A tenant-bound caller sees only its own entity changes.
//   - An absent tenant context with no host-access signal fails CLOSED to the
//     host partition (TenantId == null rows only) — never every tenant's rows.
//   - A signaled host-access request bypasses the filter and reads cross-tenant.
//   - The model-level filter on AuditingDbContext.AuditEntityChanges itself
//     scopes a tenant-bound context (core assertion independent of the scope).
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext and queryable source

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class EfAuditEntityChangeQueryableSourceTenantTests : IAsyncDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AuditingDbContext> _dbOptions;

    public EfAuditEntityChangeQueryableSourceTenantTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseSqlite(_connection)
            .Options;

        using AuditingDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        context.Database.EnsureCreated();

        // Seed one entry + entity change per partition. TenantId is stamped on BOTH
        // AuditEntry and AuditEntityChange, exactly as AuditingBatchMapper denormalizes it.
        context.AuditEntries.Add(CreateEntry(_tenantA, "EntityA"));
        context.AuditEntries.Add(CreateEntry(_tenantB, "EntityB"));
        context.AuditEntries.Add(CreateEntry(tenantId: null, "EntityHost"));
        context.SaveChanges();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    // -------------------------------------------------------------------------
    // Queryable source through the real TenantQueryScope
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetQueryable_WithTenantBoundCaller_ReturnsOnlyThatTenantsRows()
    {
        // Arrange
        ICurrentTenant currentTenant = TenantContext(_tenantA);
        await using ServiceProvider provider = BuildProvider(currentTenant);
        using IServiceScope scope = provider.CreateScope();
        await using EfAuditEntityChangeQueryableSource source = new(
            new StubAuditingDbContextFactory(_dbOptions, currentTenant),
            scope.ServiceProvider.GetRequiredService<ITenantQueryScope>());

        // Act
        List<AuditEntityChange> rows = await source.GetQueryable()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        AuditEntityChange row = rows.ShouldHaveSingleItem();
        row.TenantId.ShouldBe(_tenantA);
        row.EntityType.ShouldBe("EntityA");
    }

    [Fact]
    public async Task GetQueryable_WithNoTenantAndNoHostSignal_FailsClosedToHostPartition()
    {
        // Arrange — ICurrentTenant.IsAvailable is false and no IHostAccessContext is
        // registered: per CrossTenantFilterDecision this is an UNSIGNALED tenant-context
        // loss and must fail CLOSED — the filter stays active with CurrentTenantId null,
        // so only host-partition rows (TenantId == null) are visible, never tenant data.
        ICurrentTenant currentTenant = TenantContext(tenantId: null);
        await using ServiceProvider provider = BuildProvider(currentTenant);
        using IServiceScope scope = provider.CreateScope();
        await using EfAuditEntityChangeQueryableSource source = new(
            new StubAuditingDbContextFactory(_dbOptions, currentTenant),
            scope.ServiceProvider.GetRequiredService<ITenantQueryScope>());

        // Act
        List<AuditEntityChange> rows = await source.GetQueryable()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        AuditEntityChange row = rows.ShouldHaveSingleItem();
        row.TenantId.ShouldBeNull();
        row.EntityType.ShouldBe("EntityHost");
    }

    [Fact]
    public async Task GetQueryable_WithSignaledHostAccess_ReadsCrossTenant()
    {
        // Arrange — same absent tenant, but the request carries the explicit
        // host-access signal (.AllowHostAccess() route): the multi-tenant named
        // filter is bypassed and the host admin reads across tenants.
        ICurrentTenant currentTenant = TenantContext(tenantId: null);
        IHostAccessContext hostAccess = Substitute.For<IHostAccessContext>();
        hostAccess.IsHostAccess.Returns(true);
        await using ServiceProvider provider = BuildProvider(currentTenant, hostAccess);
        using IServiceScope scope = provider.CreateScope();
        await using EfAuditEntityChangeQueryableSource source = new(
            new StubAuditingDbContextFactory(_dbOptions, currentTenant),
            scope.ServiceProvider.GetRequiredService<ITenantQueryScope>());

        // Act
        List<AuditEntityChange> rows = await source.GetQueryable()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert — all three partitions visible.
        rows.Count.ShouldBe(3);
        rows.ShouldContain(r => r.TenantId == _tenantA);
        rows.ShouldContain(r => r.TenantId == _tenantB);
        rows.ShouldContain(r => r.TenantId == null);
    }

    // -------------------------------------------------------------------------
    // Model-level filter — the named multi-tenant filter on the entity itself
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AuditingDbContext_TenantBound_EntityChangesSetIsFiltered()
    {
        // Arrange — a bare Set<AuditEntityChange>() query on a tenant-bound context:
        // the denormalized TenantId means the named filter applies without joining
        // the parent AuditEntry.
        await using AuditingDbContext context = new(_dbOptions, TenantContext(_tenantA));

        // Act
        List<AuditEntityChange> rows = await context.AuditEntityChanges.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        rows.ShouldHaveSingleItem().TenantId.ShouldBe(_tenantA);
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

    /// <summary>
    /// Builds a container with the REAL <see cref="ITenantQueryScope"/> registration from
    /// <c>AddGranitPersistence()</c>, so the test exercises the production fail-closed decision.
    /// </summary>
    private static ServiceProvider BuildProvider(
        ICurrentTenant currentTenant,
        IHostAccessContext? hostAccess = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IMeterFactory>(new RecordingMeterFactory());
        services.AddSingleton(currentTenant);
        if (hostAccess is not null)
        {
            services.AddSingleton(hostAccess);
        }

        services.AddGranitPersistence();
        return services.BuildServiceProvider();
    }

    private static AuditEntry CreateEntry(Guid? tenantId, string entityType)
    {
        var entryId = Guid.NewGuid();
        return new AuditEntry
        {
            Id = entryId,
            Timestamp = new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero),
            UserId = "seed-user",
            Category = AuditCategory.DataMutation,
            TenantId = tenantId,
            CreatedAt = new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero),
            CreatedBy = "seed-user",
            EntityChanges =
            [
                new AuditEntityChange
                {
                    Id = Guid.NewGuid(),
                    AuditEntryId = entryId,
                    TenantId = tenantId,
                    EntityType = entityType,
                    EntityId = "1",
                    ChangeType = AuditChangeType.Created,
                },
            ],
        };
    }
}
