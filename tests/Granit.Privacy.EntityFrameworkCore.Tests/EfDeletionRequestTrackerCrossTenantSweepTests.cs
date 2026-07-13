// EF1001 noise suppressed at the project level — these tests intentionally
// instantiate the internal store via InternalsVisibleTo.
using Granit.Encryption;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.EntityFrameworkCore.DataDeletion.Internal;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shouldly;
using Xunit;

namespace Granit.Privacy.EntityFrameworkCore.Tests;

/// <summary>
/// Locks the cross-tenant SWEEP invariant for <see cref="EfDeletionRequestTracker{TContext}"/>.
/// The daily deadline-enforcer job runs with no ambient tenant, so
/// <c>GetExpiredDeferredAsync</c> must see every tenant's expired deferred deletions — the
/// inverse of the export-checkpoint store's single-tenant isolation. If a future refactor drops
/// the <c>QueryAcrossTenants</c> bypass and falls back to the implicit tenant filter, the sweep
/// silently collapses to the host partition (<c>TenantId == null</c>) and these tests fail loudly
/// — that is the exact GDPR Art. 17 gap this guard exists to catch.
/// </summary>
public sealed class EfDeletionRequestTrackerCrossTenantSweepTests
{
    private static readonly Guid TenantA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetExpiredDeferredAsync_WithNoAmbientTenant_ReturnsExpiredRowsFromEveryTenant()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // One expired deferred request per tenant — a multi-tenant deployment.
        await h.SeedAsync(TenantA, NewRequest(DeletionRequestState.Deferred, now.AddDays(-1)), ct);
        await h.SeedAsync(TenantB, NewRequest(DeletionRequestState.Deferred, now.AddDays(-2)), ct);

        // The daily enforcer runs with NO ambient tenant (NullTenantContext in production).
        var sut = new EfDeletionRequestTracker<PrivacyDbContext>(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<DeletionRequestStatus> expired = await sut.GetExpiredDeferredAsync(now, ct);

        // Both tenants' rows must surface — not just the host partition.
        expired.Select(r => r.TenantId).ShouldBe([TenantA, TenantB], ignoreOrder: true);
    }

    [Fact]
    public async Task GetExpiredDeferredAsync_ExcludesNonExpiredAndNonDeferredRows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqliteHarness h = await SqliteHarness.CreateAsync(ct);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DeletionRequestEntity expiredDeferred = NewRequest(DeletionRequestState.Deferred, now.AddDays(-1));
        await h.SeedAsync(TenantA, expiredDeferred, ct);
        // Deferred but not yet due → excluded.
        await h.SeedAsync(TenantA, NewRequest(DeletionRequestState.Deferred, now.AddDays(5)), ct);
        // Past the deadline but already executed → excluded.
        await h.SeedAsync(TenantB, NewRequest(DeletionRequestState.Executed, now.AddDays(-3)), ct);

        var sut = new EfDeletionRequestTracker<PrivacyDbContext>(h.Factory, new FakeCurrentTenant());

        IReadOnlyList<DeletionRequestStatus> expired = await sut.GetExpiredDeferredAsync(now, ct);

        expired.Select(r => r.RequestId).ShouldBe([expiredDeferred.Id]);
    }

    private static DeletionRequestEntity NewRequest(DeletionRequestState state, DateTimeOffset scheduledAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            State = state,
            Reason = "test",
            RequestedAt = scheduledAt.AddDays(-30),
            ScheduledDeletionAt = scheduledAt,
        };

    /// <summary>
    /// In-memory SQLite harness — real SQL translation (the InMemory provider lies about query
    /// filters) without a Postgres container. Seeds rows under an explicit tenant scope so the
    /// <c>GranitDbContext</c> stamps each row's <c>TenantId</c>, then runs the SUT with no ambient
    /// tenant. All contexts share the single open connection (the DB lives with the connection).
    /// </summary>
    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<PrivacyDbContext> _options;
        private readonly IStringEncryptionService _encryption = new PassthroughEncryption();

        public IDbContextFactory<PrivacyDbContext> Factory { get; }

        private SqliteHarness(SqliteConnection connection, DbContextOptions<PrivacyDbContext> options)
        {
            _connection = connection;
            _options = options;
            // Factory models the background job: no ambient tenant.
            Factory = new FixedFactory(options, _encryption);
        }

        public static async Task<SqliteHarness> CreateAsync(CancellationToken ct)
        {
            SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync(ct);

            DbContextOptions<PrivacyDbContext> options = new DbContextOptionsBuilder<PrivacyDbContext>()
                .UseSqlite(connection)
                .ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>()
                .Options;

            var harness = new SqliteHarness(connection, options);
            await using PrivacyDbContext db = new(options, harness._encryption, new FakeCurrentTenant());
            await db.Database.EnsureCreatedAsync(ct);

            return harness;
        }

        public async Task SeedAsync(Guid tenantId, DeletionRequestEntity entity, CancellationToken ct)
        {
            // TenantId is stamped by an interceptor wired at DI registration in production; the raw
            // test context has none, so set it explicitly (same approach as the checkpoint store).
            entity.TenantId = tenantId;
            await using PrivacyDbContext db = new(_options, _encryption, new FakeCurrentTenant());
            db.Set<DeletionRequestEntity>().Add(entity);
            await db.SaveChangesAsync(ct);
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

        private sealed class FixedFactory(
            DbContextOptions<PrivacyDbContext> options,
            IStringEncryptionService encryption) : IDbContextFactory<PrivacyDbContext>
        {
            public PrivacyDbContext CreateDbContext() => new(options, encryption, new FakeCurrentTenant());
            public Task<PrivacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new PrivacyDbContext(options, encryption, new FakeCurrentTenant()));
        }

        private sealed class PassthroughEncryption : IStringEncryptionService
        {
            public string Encrypt(string plainText) => plainText;
            public string? Decrypt(string cipherText) => cipherText;
        }
    }

    /// <summary>
    /// Remaps PostgreSQL-specific <see cref="DateTimeOffset"/> columns to SQLite-compatible
    /// integers so the <c>ScheduledDeletionAt &lt;= now</c> filter translates. Same shape as the
    /// companion <c>TestDbContextFactory</c> customizers across the other EF Core test projects.
    /// </summary>
    private sealed class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
        : RelationalModelCustomizer(dependencies)
    {
        private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter = new(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter = new(
            v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
            v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (IMutableProperty property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(DateTimeOffsetConverter);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(NullableDateTimeOffsetConverter);
                    }
                }
            }
        }
    }
}
