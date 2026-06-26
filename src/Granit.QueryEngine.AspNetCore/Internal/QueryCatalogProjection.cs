using Granit.QueryEngine.AspNetCore.Dtos;

namespace Granit.QueryEngine.AspNetCore.Internal;

/// <summary>
/// Maps <see cref="IQueryDefinitionDescriptor"/> instances to the wire shape
/// (<see cref="QueryCatalogEntryResponse"/>), resolving each entry's base path from a
/// pre-built entity-type → route-path index. Extracted to its own class so the projection
/// logic is unit-testable without the full WebApplication harness.
/// </summary>
internal static class QueryCatalogProjection
{
    /// <summary>
    /// Projects <paramref name="descriptors"/> (already ordered by the registry) into catalog
    /// entries. <paramref name="basePathByEntityType"/> maps an entity CLR type to the resolved
    /// base path of its <c>List</c> endpoint; a descriptor whose entity type is absent gets a
    /// <c>null</c> base path — never a forged URL. <paramref name="resolveLabel"/> produces the
    /// human-facing dropdown label for a descriptor (the localized <c>"Query:{Name}"</c> string,
    /// or the raw <c>Name</c> as fallback).
    /// </summary>
    public static IReadOnlyList<QueryCatalogEntryResponse> Project(
        IEnumerable<IQueryDefinitionDescriptor> descriptors,
        IReadOnlyDictionary<Type, string> basePathByEntityType,
        Func<IQueryDefinitionDescriptor, string> resolveLabel)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(basePathByEntityType);
        ArgumentNullException.ThrowIfNull(resolveLabel);

        return [.. descriptors.Select(d => ToResponse(d, basePathByEntityType, resolveLabel))];
    }

    private static QueryCatalogEntryResponse ToResponse(
        IQueryDefinitionDescriptor descriptor,
        IReadOnlyDictionary<Type, string> basePathByEntityType,
        Func<IQueryDefinitionDescriptor, string> resolveLabel)
    {
        string? basePath = basePathByEntityType.TryGetValue(descriptor.EntityType, out string? path)
            ? path
            : null;

        return new QueryCatalogEntryResponse(descriptor.Name, basePath, resolveLabel(descriptor));
    }
}
