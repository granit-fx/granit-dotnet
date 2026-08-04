using Granit.DataFiltering;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Testing.Fakes;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Testing.Persistence;

/// <summary>
/// Per-test harness: a real DI container wired exactly like a Granit host
/// (<c>AddGranitPersistence()</c> + <c>AddGranitDbContext&lt;ConformanceDbContext&gt;()</c>)
/// with controllable fakes for tenant, user, and clock. Every suite test creates its own
/// harness so fake state never leaks between tests; all harnesses share the fixture's
/// database (rows are isolated per test via generated ids and labels).
/// </summary>
public sealed class ConformanceHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    /// <summary>Controls <see cref="ICurrentTenant"/> seen by filters and interceptors.</summary>
    public FakeCurrentTenant CurrentTenant { get; }

    /// <summary>Controls <see cref="ICurrentUserService"/> seen by the audit interceptor.</summary>
    public FakeCurrentUser CurrentUser { get; }

    /// <summary>Controls <see cref="IClock"/> seen by the audit/soft-delete interceptors.</summary>
    public FakeClock Clock { get; }

    /// <summary>The scoped data filter (per-flow filter bypass).</summary>
    public IDataFilter DataFilter { get; }

    private ConformanceHarness(
        ServiceProvider provider,
        AsyncServiceScope scope,
        FakeCurrentTenant currentTenant,
        FakeCurrentUser currentUser,
        FakeClock clock,
        IDataFilter dataFilter)
    {
        _provider = provider;
        _scope = scope;
        CurrentTenant = currentTenant;
        CurrentUser = currentUser;
        Clock = clock;
        DataFilter = dataFilter;
    }

    /// <summary>Creates a fresh tracking context from the scoped factory.</summary>
    public Task<ConformanceDbContext> CreateContextAsync(CancellationToken ct = default) =>
        _scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ConformanceDbContext>>()
            .CreateDbContextAsync(ct);

    /// <summary>
    /// Builds the harness and ensures the conformance schema exists in the fixture database
    /// (idempotent — first harness creates it, later ones no-op).
    /// </summary>
    public static async Task<ConformanceHarness> CreateAsync(
        IRelationalConformanceFixture fixture,
        CancellationToken ct = default)
    {
        FakeCurrentTenant tenant = new();
        FakeCurrentUser user = new();
        FakeClock clock = new() { Now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero) };

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<ICurrentTenant>(tenant);
        services.AddSingleton<ICurrentUserService>(user);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IGuidGenerator, SimpleGuidGenerator>();
        services.AddGranitPersistence();
        services.AddGranitDbContext<ConformanceDbContext>(fixture.UseProvider);

        ServiceProvider provider = services.BuildServiceProvider();
        AsyncServiceScope scope = provider.CreateAsyncScope();

        ConformanceHarness harness = new(
            provider, scope, tenant, user, clock,
            provider.GetRequiredService<IDataFilter>());

        await using ConformanceDbContext db = await harness.CreateContextAsync(ct).ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);

        return harness;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync().ConfigureAwait(false);
        await _provider.DisposeAsync().ConfigureAwait(false);
    }
}
