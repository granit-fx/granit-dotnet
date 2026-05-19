// =============================================================================
// Tests d'intégration - TenantPerDatabaseDbContextFactory — routage physique par tenant
// =============================================================================
// Valide l'isolation physique de données :
//   - Tenant A écrit dans la base A uniquement.
//   - Tenant B écrit dans la base B uniquement.
//   - Aucune fuite cross-tenant.
//
// Nécessite Docker (Testcontainers spin up deux conteneurs SQL Server).
// Les conteneurs sont partagés sur la classe (IClassFixture) pour amortir le coût
// de démarrage (~15 s) sur l'ensemble des tests.
// =============================================================================

using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Testing.Containers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Testcontainers.MsSql;
using Xunit;

namespace Granit.Wolverine.SqlServer.Tests.Integration;

// ---------------------------------------------------------------------------
// DbContext & entity used exclusively by integration tests
// ---------------------------------------------------------------------------

internal sealed class TenantRecord
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
}

internal sealed class TenantIntegrationDbContext(
    DbContextOptions<TenantIntegrationDbContext> options) : DbContext(options)
{
    public DbSet<TenantRecord> Records => Set<TenantRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<TenantRecord>().ToTable("TenantRecords");
}

// ---------------------------------------------------------------------------
// Shared fixture — starts the two SQL Server containers once per test class
// ---------------------------------------------------------------------------

public sealed class TwoSqlServerContainersFixture : IAsyncLifetime
{
    private MsSqlContainer? _containerA;
    private MsSqlContainer? _containerB;

    public string ConnectionStringA { get; private set; } = string.Empty;
    public string ConnectionStringB { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        string? ciHost = Environment.GetEnvironmentVariable("MSSQL_HOST");

        if (!string.IsNullOrEmpty(ciHost))
        {
            // CI mode: use the SQL Server service provided by CI.
            // Create two separate databases on the same server.
            string port = Environment.GetEnvironmentVariable("MSSQL_PORT") ?? "1433";
            string user = Environment.GetEnvironmentVariable("MSSQL_USER") ?? "sa";
            string password = Environment.GetEnvironmentVariable("MSSQL_PASSWORD") ?? "Test_Password1!";

            string adminConnectionString =
                $"Server={ciHost},{port};Database=master;User Id={user};Password={password};TrustServerCertificate=True";

            await CreateDatabaseAsync(adminConnectionString, "tenant_a");
            await CreateDatabaseAsync(adminConnectionString, "tenant_b");

            ConnectionStringA = $"Server={ciHost},{port};Database=tenant_a;User Id={user};Password={password};TrustServerCertificate=True";
            ConnectionStringB = $"Server={ciHost},{port};Database=tenant_b;User Id={user};Password={password};TrustServerCertificate=True";
        }
        else
        {
            // Local mode: use Testcontainers (requires Docker).
            _containerA = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword("Test_Password1!")
                .Build();

            _containerB = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword("Test_Password1!")
                .Build();

            // MCR (mcr.microsoft.com/mssql/server) has been observed to intermittently
            // block pulls from CI runners via Azure Front Door. Retry up to 3 times
            // with exponential backoff so a single transient block doesn't fail the job.
            await ContainerStartRetry.RunWithRetryAsync(
                ct => Task.WhenAll(_containerA.StartAsync(ct), _containerB.StartAsync(ct)),
                label: "mssql-per-tenant-fixture");

            ConnectionStringA = _containerA.GetConnectionString();
            ConnectionStringB = _containerB.GetConnectionString();
        }

        await MigrateAsync(ConnectionStringA);
        await MigrateAsync(ConnectionStringB);
    }

    public async ValueTask DisposeAsync()
    {
        if (_containerA is not null)
        {
            await _containerA.DisposeAsync();
        }

        if (_containerB is not null)
        {
            await _containerB.DisposeAsync();
        }
    }

    private static async Task CreateDatabaseAsync(string adminConnectionString, string dbName)
    {
        // Retry to handle transient networking delays when the CI SQL Server service is starting.
        const int maxAttempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using SqlConnection conn = new(adminConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                await using SqlCommand checkCmd = conn.CreateCommand();
                checkCmd.CommandText = $"SELECT DB_ID('{dbName}')";
                object? exists = await checkCmd.ExecuteScalarAsync().ConfigureAwait(false);

                if (exists is null or DBNull)
                {
                    await using SqlCommand createCmd = conn.CreateCommand();
                    createCmd.CommandText = $"CREATE DATABASE [{dbName}]";
                    await createCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                return;
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(attempt * 1_000).ConfigureAwait(false);
            }
        }
    }

    private static async Task MigrateAsync(string connectionString)
    {
        DbContextOptions<TenantIntegrationDbContext> opts =
            new DbContextOptionsBuilder<TenantIntegrationDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        // Retry to handle transient networking delays after service startup.
        const int maxAttempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using TenantIntegrationDbContext ctx = new(opts);
                await ctx.Database.EnsureCreatedAsync().ConfigureAwait(false);
                return;
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(attempt * 1_000).ConfigureAwait(false);
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

[Collection("sqlserver-integration")]
public sealed class PerTenantRoutingTests(TwoSqlServerContainersFixture fixture)
    : IClassFixture<TwoSqlServerContainersFixture>
{
    private static readonly Guid TenantAId = Guid.NewGuid();
    private static readonly Guid TenantBId = Guid.NewGuid();

    private TenantPerDatabaseDbContextFactory<TenantIntegrationDbContext> BuildFactory(
        Guid activeTenantId)
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(activeTenantId);

        ITenantConnectionStringProvider provider =
            Substitute.For<ITenantConnectionStringProvider>();
        provider
            .GetConnectionStringAsync(TenantAId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(fixture.ConnectionStringA));
        provider
            .GetConnectionStringAsync(TenantBId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(fixture.ConnectionStringB));

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TenantPerDatabaseDbContextOptions<TenantIntegrationDbContext> options = new()
        {
            Configure = static (opts, cs) => opts.UseSqlServer(cs),
        };

        return new TenantPerDatabaseDbContextFactory<TenantIntegrationDbContext>(
            currentTenant, provider, sp, options);
    }

    // -----------------------------------------------------------------------
    // Tenant A écrit dans la base A
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Write_WithTenantA_InsertsInDatabaseA()
    {
        TenantPerDatabaseDbContextFactory<TenantIntegrationDbContext> factory =
            BuildFactory(TenantAId);

        await using TenantIntegrationDbContext ctx =
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        ctx.Records.Add(new TenantRecord { Value = "record-tenant-a" });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<TenantRecord> rows =
            await ctx.Records.ToListAsync(TestContext.Current.CancellationToken);
        rows.ShouldContain(r => r.Value == "record-tenant-a");
    }

    // -----------------------------------------------------------------------
    // Tenant B écrit dans la base B
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Write_WithTenantB_InsertsInDatabaseB()
    {
        TenantPerDatabaseDbContextFactory<TenantIntegrationDbContext> factory =
            BuildFactory(TenantBId);

        await using TenantIntegrationDbContext ctx =
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        ctx.Records.Add(new TenantRecord { Value = "record-tenant-b" });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<TenantRecord> rows =
            await ctx.Records.ToListAsync(TestContext.Current.CancellationToken);
        rows.ShouldContain(r => r.Value == "record-tenant-b");
    }

    // -----------------------------------------------------------------------
    // Isolation physique — aucune fuite cross-tenant
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Write_WithTenantA_DoesNotAppearInDatabaseB()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Write via Tenant A.
        await using TenantIntegrationDbContext ctxA =
            await BuildFactory(TenantAId).CreateDbContextAsync(cancellationToken);
        ctxA.Records.Add(new TenantRecord { Value = "only-in-a" });
        await ctxA.SaveChangesAsync(cancellationToken);

        // Read via Tenant B — should not see the record.
        await using TenantIntegrationDbContext ctxB =
            await BuildFactory(TenantBId).CreateDbContextAsync(cancellationToken);
        List<TenantRecord> rowsInB = await ctxB.Records
            .Where(r => r.Value == "only-in-a")
            .ToListAsync(cancellationToken);

        rowsInB.ShouldBeEmpty("tenant isolation must prevent cross-tenant data leaks (ISO 27001)");
    }

    [Fact]
    public async Task Write_WithTenantB_DoesNotAppearInDatabaseA()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Write via Tenant B.
        await using TenantIntegrationDbContext ctxB =
            await BuildFactory(TenantBId).CreateDbContextAsync(cancellationToken);
        ctxB.Records.Add(new TenantRecord { Value = "only-in-b" });
        await ctxB.SaveChangesAsync(cancellationToken);

        // Read via Tenant A — should not see the record.
        await using TenantIntegrationDbContext ctxA =
            await BuildFactory(TenantAId).CreateDbContextAsync(cancellationToken);
        List<TenantRecord> rowsInA = await ctxA.Records
            .Where(r => r.Value == "only-in-b")
            .ToListAsync(cancellationToken);

        rowsInA.ShouldBeEmpty("tenant isolation must prevent cross-tenant data leaks (ISO 27001)");
    }

    // -----------------------------------------------------------------------
    // Garde — aucun tenant actif → InvalidOperationException
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_WithNoActiveTenant_ThrowsInvalidOperationException()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns((Guid?)null);

        ITenantConnectionStringProvider provider =
            Substitute.For<ITenantConnectionStringProvider>();

        ServiceCollection services = new();
        TenantPerDatabaseDbContextOptions<TenantIntegrationDbContext> options = new()
        {
            Configure = static (opts, cs) => opts.UseSqlServer(cs),
        };

        TenantPerDatabaseDbContextFactory<TenantIntegrationDbContext> factory = new(
            currentTenant, provider, services.BuildServiceProvider(), options);

        Func<Task> act = async () =>
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("No active tenant context");
    }
}
