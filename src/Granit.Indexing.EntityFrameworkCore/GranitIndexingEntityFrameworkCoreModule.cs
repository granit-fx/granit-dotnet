using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore;

/// <summary>
/// Granit module for the EF-backed indexing storage (Postgres tsvector default).
/// </summary>
/// <remarks>
/// <para>
/// Module-level wiring is a no-op: <c>AddGranitIndexingEntityFrameworkCore</c> requires
/// a per-host <see cref="Microsoft.EntityFrameworkCore.DbContextOptionsBuilder"/>
/// configuration delegate, so the module cannot self-register without input. Consumers
/// call the extension from their composition root after registering
/// <see cref="GranitIndexingModule"/>.
/// </para>
/// <para>
/// <c>[DependsOn]</c> declares the indexing base + persistence base. The GDPR Art. 17
/// cascade lives in the optional <c>Granit.Indexing.Privacy</c> bridge so this package
/// stays free of <c>Granit.Privacy</c> dependency.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitIndexingModule), typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitIndexingEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // No-op: see remarks.
    }
}
