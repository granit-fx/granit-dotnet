using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.Mergeable.Tests.Integration;

/// <summary>
/// Minimal <see cref="IDbContextFactory{TContext}"/> for the integration tests — wires
/// <see cref="PartiesDbContext"/> against the Postgres connection string from the
/// Testcontainer fixture. No multi-tenancy / data filter wiring is required here because
/// the rewriters explicitly disable the merge-tombstone filter on every operation.
/// </summary>
internal sealed class TestPartiesDbContextFactory : IDbContextFactory<PartiesDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<PartiesDbContext> _options;

    public TestPartiesDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    public PartiesDbContext CreateDbContext() => new(_options);

    public Task<PartiesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new PartiesDbContext(_options));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
