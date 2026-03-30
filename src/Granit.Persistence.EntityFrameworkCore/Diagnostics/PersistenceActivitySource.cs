using System.Diagnostics;

namespace Granit.Persistence.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Persistence.EntityFrameworkCore distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class PersistenceActivitySource
{
    /// <summary>The name of the Granit.Persistence.EntityFrameworkCore <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Persistence.EntityFrameworkCore";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────
    internal const string SaveChangesAudit = "persistence.save-changes-audit";
    internal const string SoftDelete = "persistence.soft-delete";
    internal const string DomainEventDispatch = "persistence.domain-event-dispatch";
    internal const string DataSeed = "persistence.data-seed";
    internal const string PurgeSoftDeleted = "persistence.purge-soft-deleted";
}
