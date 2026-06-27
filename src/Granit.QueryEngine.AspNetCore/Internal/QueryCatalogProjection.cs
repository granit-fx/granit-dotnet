using Granit.QueryEngine.AspNetCore.Dtos;

namespace Granit.QueryEngine.AspNetCore.Internal;

/// <summary>
/// Maps <see cref="IQueryDefinitionDescriptor"/> instances to the wire shape
/// (<see cref="QueryCatalogEntryResponse"/>), resolving each entry's base path from a
/// pre-built entity-type → route-path index and its label key from a resolver. Extracted to its
/// own class so the projection logic is unit-testable without the full WebApplication harness.
/// </summary>
internal static class QueryCatalogProjection
{
    /// <summary>
    /// Projects <paramref name="descriptors"/> (already ordered by the registry) into catalog
    /// entries. <paramref name="basePathByEntityType"/> maps an entity CLR type to the resolved
    /// base path of its <c>List</c> endpoint; a descriptor whose entity type is absent gets a
    /// <c>null</c> base path — never a forged URL. <paramref name="resolveLabelKey"/> produces the
    /// localization key the frontend resolves for the dropdown label (the target entity's
    /// display key, or <c>"Query:{Name}"</c> as fallback).
    /// </summary>
    public static IReadOnlyList<QueryCatalogEntryResponse> Project(
        IEnumerable<IQueryDefinitionDescriptor> descriptors,
        IReadOnlyDictionary<Type, string> basePathByEntityType,
        Func<IQueryDefinitionDescriptor, string> resolveLabelKey)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(basePathByEntityType);
        ArgumentNullException.ThrowIfNull(resolveLabelKey);

        return [.. descriptors.Select(d => ToResponse(d, basePathByEntityType, resolveLabelKey))];
    }

    private static QueryCatalogEntryResponse ToResponse(
        IQueryDefinitionDescriptor descriptor,
        IReadOnlyDictionary<Type, string> basePathByEntityType,
        Func<IQueryDefinitionDescriptor, string> resolveLabelKey)
    {
        string? basePath = basePathByEntityType.TryGetValue(descriptor.EntityType, out string? path)
            ? path
            : null;

        return new QueryCatalogEntryResponse(descriptor.Name, basePath, resolveLabelKey(descriptor));
    }
}
