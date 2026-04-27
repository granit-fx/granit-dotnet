using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Entities;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.Deduplication.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="PartyDuplicateCandidate"/>. Powers the query-engine-driven
/// <c>GET /parties/duplicates</c> endpoint with full filter / sort / pagination /
/// saved-views support.
/// </summary>
/// <remarks>
/// The standard <see cref="Granit.MultiTenancy.IMultiTenant"/> query filter is left
/// active deliberately — every read auto-scopes to the current tenant the same way
/// <c>EfPartyQueryableSource</c> does. Cross-tenant browsing is opt-in via
/// <c>IDataFilter.Disable&lt;IMultiTenant&gt;()</c> from a permission-gated host endpoint.
/// </remarks>
internal sealed class EfPartyDuplicateCandidateQueryableSource(
    IDbContextFactory<PartiesDbContext> contextFactory)
    : IQueryableSource<PartyDuplicateCandidate>, IDisposable
{
    private readonly PartiesDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<PartyDuplicateCandidate> GetQueryable() =>
        _context.DuplicateCandidates.AsNoTracking();

    public void Dispose() => _context.Dispose();
}
