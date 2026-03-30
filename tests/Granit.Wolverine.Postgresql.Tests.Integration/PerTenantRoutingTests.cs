// =============================================================================
// Tests d'intégration - TenantPerDatabaseDbContextFactory — routage physique par tenant
// =============================================================================
// Valide l'isolation physique de données :
//   - Tenant A écrit dans la base A uniquement.
//   - Tenant B écrit dans la base B uniquement.
//   - Aucune fuite cross-tenant.
//
// Nécessite Docker (Testcontainers spin up deux conteneurs PostgreSQL).
// Les conteneurs sont partagés sur la classe (IClassFixture) pour amortir le coût
// de démarrage (~3 s) sur l'ensemble des tests.
// =============================================================================

using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Wolverine.Postgresql.Tests.Integration;

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
        modelBuilder.Entity<TenantRecord>().ToTable("tenant_records");
}

// ---------------------------------------------------------------------------
// Shared fixture — starts the two PostgreSQL containers once per test class
// ---------------------------------------------------------------------------

public sealed class TwoPostgresContainersFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _containerA;
    private PostgreSqlContainer? _containerB;

    public string ConnectionStringA { get; private set; } = string.Empty;
    public string ConnectionStringB { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        string? ciHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");

        if (!string.IsNullOrEmpty(ciHost))
        {
            // CI mode: use the PostgreSQL service container provided by CI.
            // Create two separate databases on the same server.
            string port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
            string user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "granit_test";
            string password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "test_password";
            string mainDb = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "granit_test";

            string adminConnectionString =
                $"Host={ciHost};Port={port};Database={mainDb};Username={user};Password={password}";

            await CreateDatabaseAsync(adminConnectionString, "tenant_a");
            await CreateDatabaseAsync(adminConnectionString, "tenant_b");

            ConnectionStringA = $"Host={ciHost};Port={port};Database=tenant_a;Username={user};Password={password}";
            ConnectionStringB = $"Host={ciHost};Port={port};Database=tenant_b;Username={user};Password={password}";
        }
        else
        {
            // Local mode: use Testcontainers (requires Docker).
            _containerA = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("tenant_a")
                .WithUsername("granit")
                .WithPassword("granit_test")
                .Build();

            _containerB = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("tenant_b")
                .WithUsername("granit")
                .WithPassword("granit_test")
                .Build();

            await Task.WhenAll(_containerA.StartAsync(), _containerB.StartAsync());

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
        // Retry to handle transient DNS/networking delays when the CI PostgreSQL service is starting.
        const int maxAttempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using NpgsqlConnection conn = new(adminConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                // Check if the database already exists before creating.
                await using NpgsqlCommand checkCmd = conn.CreateCommand();
                checkCmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{dbName}'";
                object? exists = await checkCmd.ExecuteScalarAsync().ConfigureAwait(false);

                if (exists is null)
                {
                    await using NpgsqlCommand createCmd = conn.CreateCommand();
                    createCmd.CommandText = $"CREATE DATABASE {dbName}";
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
                .UseNpgsql(connectionString)
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

[Collection("postgres-integration")]
public sealed class PerTenantRoutingTests(TwoPostgresContainersFixture fixture)
    : IClassFixture<TwoPostgresContainersFixture>
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
            Configure = static (opts, cs) => opts.UseNpgsql(cs),
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
            Configure = static (opts, cs) => opts.UseNpgsql(cs),
        };

        TenantPerDatabaseDbContextFactory<TenantIntegrationDbContext> factory = new(
            currentTenant, provider, services.BuildServiceProvider(), options);

        Func<Task> act = async () =>
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("No active tenant context");
    }
}
