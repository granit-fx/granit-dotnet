using Granit.Persistence.EntityFrameworkCore.Hosting;
using Wolverine.Persistence.Durability;

namespace Granit.Wolverine.Postgresql.Internal;

/// <summary>
/// Migrates Wolverine envelope tables (incoming, outgoing, dead-letter) during <c>--migrate</c> mode.
/// </summary>
internal sealed class WolverineStoreMigrator(IMessageStore messageStore) : IExternalStoreMigrator
{
    public string Name => "Wolverine message storage";

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await messageStore.Admin.MigrateAsync().ConfigureAwait(false);
    }
}
