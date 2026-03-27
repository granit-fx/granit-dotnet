using Granit.OpenIddict.Entities.OpenIddict;
using Granit.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Creates OpenIddict and ASP.NET Identity tables when they do not exist.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OpenIddictDbContext"/> extends <c>IdentityDbContext</c> and includes OpenIddict,
/// Identity, and custom Granit entities (user groups, signing keys). All tables use the
/// <c>openiddict_</c> prefix and are fully isolated from host application tables.
/// </para>
/// <para>
/// Called by the migration runner after host migrations complete, following the same pattern
/// as <c>MigrationProgressDbEnsurer</c>. Uses <see cref="IRelationalDatabaseCreator"/> to
/// create tables from the model when they are missing.
/// </para>
/// </remarks>
internal sealed class OpenIddictDbEnsurer(
    IDbContextFactory<OpenIddictDbContext> factory) : IInternalDbContextEnsurer
{
    /// <inheritdoc/>
    public string ContextName => nameof(OpenIddictDbContext);

    /// <inheritdoc/>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IRelationalDatabaseCreator creator = db.GetService<IRelationalDatabaseCreator>();

        if (!await creator.HasTablesAsync(cancellationToken).ConfigureAwait(false))
        {
            // Empty database — create all tables from the model
            await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // Database has tables from host migrations — check if OpenIddict tables exist
        try
        {
            await db.Set<GranitOpenIddictScope>().AnyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // OpenIddict tables don't exist — create them from the model
            await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
