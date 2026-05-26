using Granit.Indexing.EntityFrameworkCore.Extensions;
using Granit.Indexing.EntityFrameworkCore.Options;
using Granit.MultiTenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore.Tests;

/// <summary>
/// In-memory SQLite harness — gives us real SQL translation (the InMemory provider lies
/// about query filters) without a Postgres container. tsvector is unmapped on this
/// provider; the harness exercises tenant filter parameterisation, upsert behaviour, and
/// the GDPR eraser delete path.
/// </summary>
internal sealed class SqliteHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public IServiceProvider Services { get; }

    public IDbContextFactory<IndexingDbContext> Factory =>
        Services.GetRequiredService<IDbContextFactory<IndexingDbContext>>();

    private SqliteHarness(SqliteConnection connection, IServiceProvider sp)
    {
        _connection = connection;
        Services = sp;
    }

    public static async Task<SqliteHarness> CreateAsync(MutableTenant tenant, CancellationToken ct, params Type[] indexedKeyTypes)
    {
        SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync(ct);

        ServiceCollection services = [];
        services.AddSingleton<ICurrentTenant>(tenant);
        services.AddSingleton<Granit.Indexing.Diagnostics.IndexingMetrics>(sp =>
            new Granit.Indexing.Diagnostics.IndexingMetrics(sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>()));
        services.AddMetrics();
        services.AddSingleton<Granit.Events.ILocalEventBus, NoopLocalEventBus>();

        services.AddGranitIndexingEntityFrameworkCore(
            opts => opts.UseSqlite(connection),
            indexedKeyTypes);

        services.AddSingleton(new IndexingEntityFrameworkCoreOptions());

        ServiceProvider sp = services.BuildServiceProvider();
        await using (IndexingDbContext db = await sp.GetRequiredService<IDbContextFactory<IndexingDbContext>>().CreateDbContextAsync(ct))
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        return new SqliteHarness(connection, sp);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Services is IAsyncDisposable a)
        {
            await a.DisposeAsync();
        }
        else if (Services is IDisposable s)
        {
            s.Dispose();
        }
    }
}

internal sealed class MutableTenant : ICurrentTenant
{
    public Guid? Id { get; set; }
    public bool IsAvailable => Id is not null;
    public string? Name => null;
    public IDisposable Change(Guid? id, string? name = null)
    {
        Guid? prev = Id;
        Id = id;
        return new Restore(this, prev);
    }

    private sealed class Restore(MutableTenant t, Guid? prev) : IDisposable
    {
        public void Dispose() => t.Id = prev;
    }
}

internal sealed class NoopLocalEventBus : Granit.Events.ILocalEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class =>
        Task.CompletedTask;
}
