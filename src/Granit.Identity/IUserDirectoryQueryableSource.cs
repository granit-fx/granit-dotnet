using Granit.Identity.Domain;

namespace Granit.Identity;

/// <summary>
/// Provides an <see cref="IQueryable{User}"/> source for the user directory
/// — admin grids, OData feed, BI exports. Per ADR-051 the
/// <see cref="User"/> aggregate replaces the dual <c>GranitUser</c> /
/// <c>UserCacheEntry</c> read paths with a single canonical surface.
/// </summary>
/// <remarks>
/// <para>
/// Implementations live in persistence-layer companion packages (e.g.
/// <c>Granit.Identity.EntityFrameworkCore</c>) and return a queryable
/// already-filtered by the framework conventions (tenant + soft-delete via
/// <c>ApplyGranitConventions</c>).
/// </para>
/// <para>
/// Distinct from <see cref="IUserLookupService"/> which targets point lookups
/// (<c>FindByIdAsync</c> / <c>SearchAsync</c>) — this contract is for
/// composable queries (<c>$filter</c> / <c>$select</c> / <c>$top</c> /
/// <c>$orderby</c>) consumed by the OData layer per ADR-050.
/// </para>
/// </remarks>
public interface IUserDirectoryQueryableSource
{
    /// <summary>
    /// Returns the base <see cref="IQueryable{User}"/> for the
    /// <see cref="User"/> aggregate. The QueryEngine and OData pipelines
    /// compose <c>$filter</c> / <c>$select</c> / <c>$top</c> / <c>$orderby</c>
    /// on top.
    /// </summary>
    IQueryable<User> GetQueryable();
}
