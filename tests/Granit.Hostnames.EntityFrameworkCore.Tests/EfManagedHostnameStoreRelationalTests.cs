using System.Diagnostics.Metrics;
using Granit.Hostnames.Diagnostics;
using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.EntityFrameworkCore.Tests;

/// <summary>
/// Relational (SQLite) coverage for queries that must translate to SQL. The companion
/// <see cref="EfManagedHostnameStoreTests"/> uses the in-memory provider, which client-evaluates
/// every expression and therefore cannot catch LINQ-translation failures — exactly the class of
/// bug this suite guards against (e.g. ordering through a value-converted property's member).
/// </summary>
public sealed class EfManagedHostnameStoreRelationalTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ListByOwnerAsync_orders_by_host_and_filters_owner()
    {
        // The Host value object is persisted via a ValueConverter (whole-value round-trip).
        // Ordering must target the converted property itself; reaching into Host.Value is not
        // translatable and throws against a relational provider. Regression for the CMS
        // GET /sites/{id}/hostnames 500 (InvalidOperationException: could not be translated).
        using var factory = SqliteHostnamesContextFactory.Create(Tenant);
        EfManagedHostnameStore store = CreateStore(factory);
        var otherOwner = Guid.NewGuid();

        CancellationToken ct = TestContext.Current.CancellationToken;
        await store.AddAsync(MakeHostname("zeta.example.com"), ct);
        await store.AddAsync(MakeHostname("alpha.example.com"), ct);
        await store.AddAsync(MakeHostname("mid.example.com"), ct);
        await store.AddAsync(MakeHostname("other.example.com", otherOwner), ct);

        IReadOnlyList<ManagedHostname> result =
            await store.ListByOwnerAsync("cms.site", OwnerId, cancellationToken: ct);

        // Only the queried owner's rows, ascending by host.
        result.Select(h => h.Host.Value)
            .ShouldBe(["alpha.example.com", "mid.example.com", "zeta.example.com"]);
    }

    private static EfManagedHostnameStore CreateStore(SqliteHostnamesContextFactory factory)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(Tenant);

        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));

        return new EfManagedHostnameStore(factory, tenant, new HostnamesMetrics(meterFactory));
    }

    private static ManagedHostname MakeHostname(string host, Guid? ownerId = null) =>
        ManagedHostname.Create(Guid.NewGuid(), host, "cms.site", ownerId ?? OwnerId, Tenant, isPrimary: false);

    private sealed class SqliteHostnamesContextFactory(
        SqliteConnection connection,
        DbContextOptions<HostnamesDbContext> options,
        ICurrentTenant tenant)
        : IDbContextFactory<HostnamesDbContext>, IDisposable
    {
        public static SqliteHostnamesContextFactory Create(Guid tenantId)
        {
            ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
            tenant.IsAvailable.Returns(true);
            tenant.Id.Returns(tenantId);

            SqliteConnection connection = new("DataSource=:memory:");
            connection.Open();

            DbContextOptionsBuilder<HostnamesDbContext> optionsBuilder = new();
            optionsBuilder.UseSqlite(connection);
            optionsBuilder.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();
            DbContextOptions<HostnamesDbContext> options = optionsBuilder.Options;

            using (HostnamesDbContext db = new(options, tenant))
            {
                db.Database.EnsureCreated();
            }

            return new SqliteHostnamesContextFactory(connection, options, tenant);
        }

        public HostnamesDbContext CreateDbContext() => new(options, tenant);

        public Task<HostnamesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        public void Dispose() => connection.Dispose();
    }

    /// <summary>Remaps PostgreSQL-specific CLR types to SQLite-compatible ones — same shape as
    /// the companion <c>*.EntityFrameworkCore.Tests</c> factories.</summary>
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
