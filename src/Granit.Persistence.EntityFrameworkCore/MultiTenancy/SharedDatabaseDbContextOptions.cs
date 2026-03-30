using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

internal sealed class SharedDatabaseDbContextOptions<TContext>
    where TContext : DbContext
{
    public required Action<DbContextOptionsBuilder<TContext>> Configure { get; init; }
}
