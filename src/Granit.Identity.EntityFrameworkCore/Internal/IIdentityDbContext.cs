using Granit.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Common shape exposed by every Identity DbContext flavour so stores can dispatch reads
/// and writes regardless of whether the storage layout is <c>Shared</c> (one context) or
/// <c>Segregated</c> (host context + tenant context).
/// </summary>
/// <remarks>
/// Per ADR-063 the two concrete implementations are:
/// <list type="bullet">
///   <item><see cref="IdentityHostDbContext"/> — host-pinned. Serves both <c>Shared</c> mode (single context for all rows, row-level filter) and the host portion of <c>Segregated</c> mode (host-admin users only).</item>
///   <item><see cref="IdentityTenantDbContext"/> — tenant-isolated. Used only under <c>Segregated</c> mode for per-tenant users.</item>
/// </list>
/// </remarks>
internal interface IIdentityDbContext : IAsyncDisposable, IDisposable
{
    /// <summary>Users in the current scope.</summary>
    DbSet<User> Users { get; }

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
