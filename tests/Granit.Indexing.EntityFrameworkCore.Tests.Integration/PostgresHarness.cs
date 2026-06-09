using Granit.Events;
using Granit.Indexing.EntityFrameworkCore.Extensions;
using Granit.Indexing.EntityFrameworkCore.Options;
using Granit.MultiTenancy;
using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Test harness wiring <see cref="IndexingDbContext"/> against a real Postgres container.
/// Each instance lives in its own schema so suites stay isolated.
/// </summary>
internal sealed class PostgresHarness : IAsyncDisposable
{
    public IServiceProvider Services { get; }
    public IDbContextFactory<IndexingDbContext> Factory =>
        Services.GetRequiredService<IDbContextFactory<IndexingDbContext>>();

    private PostgresHarness(IServiceProvider sp) => Services = sp;

    public static async Task<PostgresHarness> CreateAsync(
        string connectionString,
        FakeCurrentTenant tenant,
        CancellationToken ct,
        params Type[] indexedKeyTypes)
    {
        ServiceCollection services = [];
        services.AddSingleton<ICurrentTenant>(tenant);
        services.AddMetrics();
        services.AddSingleton<Granit.Indexing.Diagnostics.IndexingMetrics>(sp =>
            new Granit.Indexing.Diagnostics.IndexingMetrics(sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>()));
        services.AddSingleton<ILocalEventBus, NoopLocalEventBus>();
        services.AddSingleton(new IndexingEntityFrameworkCoreOptions());

        services.AddGranitIndexingEntityFrameworkCore(
            opts => opts.UseNpgsql(connectionString),
            indexedKeyTypes);

        // Default projection used by integration tests: project to the row's content.
        services.AddGranitIndexingBackend<Guid, string>(r => r.Content);

        ServiceProvider sp = services.BuildServiceProvider();
        await using (IndexingDbContext db = await sp.GetRequiredService<IDbContextFactory<IndexingDbContext>>().CreateDbContextAsync(ct))
        {
            // Idempotent across tests sharing the same container — schema is created once,
            // rows are truncated below to guarantee isolation.
            await db.Database.EnsureCreatedAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                $"TRUNCATE TABLE \"{GranitIndexingDbProperties.DbTablePrefix}indexed_entry_guid\"", ct);
        }

        return new PostgresHarness(sp);
    }

    public async ValueTask DisposeAsync()
    {
        if (Services is IAsyncDisposable a) { await a.DisposeAsync(); }
        else if (Services is IDisposable s) { s.Dispose(); }
    }
}

internal sealed class NoopLocalEventBus : ILocalEventBus
{
    public Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default) where TEvent : class =>
        Task.CompletedTask;
}
