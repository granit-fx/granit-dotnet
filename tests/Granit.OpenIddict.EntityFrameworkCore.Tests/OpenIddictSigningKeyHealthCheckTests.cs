using Granit.MultiTenancy;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.EntityFrameworkCore.HealthChecks;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Options;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

/// <summary>
/// The signing-key health check reports DbContext readiness and signing-key availability from a
/// single query. Exercised against SQLite so the actual EF query path (and a genuinely absent
/// schema) drive the result, not a substituted store.
/// </summary>
public sealed class OpenIddictSigningKeyHealthCheckTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Healthy_WhenRotationEnabled_AndActiveKeyPresent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        OpenIddictSigningKeyHealthCheck check = BuildCheck(rotationEnabled: true);
        await CreateSchemaAsync(ct);
        await SeedActiveKeyAsync(ct);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Unhealthy_WhenRotationEnabled_ButNoActiveKey()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        OpenIddictSigningKeyHealthCheck check = BuildCheck(rotationEnabled: true);
        await CreateSchemaAsync(ct);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("no active", Case.Insensitive);
    }

    [Fact]
    public async Task Healthy_WhenRotationDisabled_AndTableEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        OpenIddictSigningKeyHealthCheck check = BuildCheck(rotationEnabled: false);
        await CreateSchemaAsync(ct);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Unhealthy_WhenSchemaMissing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        // No CreateSchemaAsync: the signing-key table does not exist, standing in for an
        // unmigrated or unreachable database.
        OpenIddictSigningKeyHealthCheck check = BuildCheck(rotationEnabled: true);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("unreachable", Case.Insensitive);
    }

    private OpenIddictSigningKeyHealthCheck BuildCheck(bool rotationEnabled)
    {
        ServiceCollection services = new();
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        services.AddDbContextFactory<OpenIddictDbContext>(options => options.UseSqlite(_connection));
        services.AddOptions<GranitKeyRotationOptions>().Configure(o => o.Enabled = rotationEnabled);

        _provider = services.BuildServiceProvider();

        return new OpenIddictSigningKeyHealthCheck(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _provider.GetRequiredService<IOptions<GranitKeyRotationOptions>>());
    }

    private async Task CreateSchemaAsync(CancellationToken ct)
    {
        IDbContextFactory<OpenIddictDbContext> factory =
            _provider.GetRequiredService<IDbContextFactory<OpenIddictDbContext>>();
        await using OpenIddictDbContext db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
    }

    private async Task SeedActiveKeyAsync(CancellationToken ct)
    {
        IDbContextFactory<OpenIddictDbContext> factory =
            _provider.GetRequiredService<IDbContextFactory<OpenIddictDbContext>>();
        await using OpenIddictDbContext db = await factory.CreateDbContextAsync(ct);
        db.SigningKeys.Add(SigningKey.Create(
            keyId: "kid-1",
            keyType: "signing",
            algorithm: "RS256",
            encryptedKeyMaterial: "cipher",
            activatedAt: DateTimeOffset.UnixEpoch,
            expiresAt: DateTimeOffset.UnixEpoch.AddDays(90)));
        await db.SaveChangesAsync(ct);
    }
}
