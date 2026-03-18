using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.Migrations.Internal;

/// <summary>
/// Creates the migration progress tracking table via <see cref="MigrationProgressDbContext"/>.
/// </summary>
internal sealed class MigrationProgressDbEnsurer(
    IDbContextFactory<MigrationProgressDbContext> factory) : IMigrationProgressDbEnsurer
{
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using MigrationProgressDbContext dbContext = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
