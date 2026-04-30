using Granit.Entities.Endpoints.Dtos;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Parses the <c>?facets=</c> query parameter into an
/// <see cref="EntityFacets"/> flag set. Wire form: comma-separated kebab-case
/// names. An empty / missing value resolves to <see cref="EntityFacets.All"/>.
/// </summary>
internal static class EntityFacetParser
{
    public static EntityFacets Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return EntityFacets.All;
        }

        EntityFacets facets = EntityFacets.None;
        foreach (string token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            facets |= token.ToLowerInvariant() switch
            {
                "identity" => EntityFacets.Identity,
                "permissions" => EntityFacets.Permissions,
                "form" or "forms" => EntityFacets.Forms,
                "detail" or "details" => EntityFacets.Details,
                "collection" or "collections" or "list" => EntityFacets.Collections,
                "dashboard" or "dashboards" => EntityFacets.Dashboards,
                "export" or "exports" => EntityFacets.Exports,
                "view" or "views" or "saved-views" => EntityFacets.Views,
                _ => EntityFacets.None,
            };
        }

        return facets == EntityFacets.None ? EntityFacets.All : facets;
    }
}
