using Granit.Mergeable.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.Mergeable.Tests.Integration;

/// <summary>
/// Minimal <see cref="IDbContextFactory{TContext}"/> for the merge-orchestrator's
/// <c>granit.merge_idempotency</c> bookkeeping table — wires the internal
/// <see cref="MergeableDbContext"/> against the Postgres connection string from the
/// Testcontainer fixture. Visible from this assembly via the <c>InternalsVisibleTo</c>
/// entry on <c>Granit.Mergeable.EntityFrameworkCore.csproj</c>.
/// </summary>
internal sealed class TestMergeableDbContextFactory : IDbContextFactory<MergeableDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<MergeableDbContext> _options;

    public TestMergeableDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<MergeableDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    public MergeableDbContext CreateDbContext() => new(_options);

    public Task<MergeableDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MergeableDbContext(_options));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
