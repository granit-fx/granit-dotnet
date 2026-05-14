namespace Granit.Entities;

/// <summary>
/// Discriminates the kind of admin endpoint marked by an
/// <see cref="EntityEndpointMetadata"/>. Values are stable wire-level
/// identifiers — adding a kind is non-breaking; renaming one is breaking.
/// </summary>
public enum EntityEndpointKind
{
    /// <summary>
    /// Paginated query / collection endpoint. By convention the frontend calls
    /// <c>{path}</c> for the page and <c>{path}/meta</c> for the column metadata.
    /// </summary>
    List = 0,
}

/// <summary>
/// Endpoint metadata tag that links a route to an entity CLR type.
/// </summary>
/// <remarks>
/// <para>
/// Attached at <c>Map*</c> time via the ASP.NET Core
/// <c>IEndpointConventionBuilder.WithMetadata</c> extension; read by the
/// entity-discovery surface (<c>Granit.Entities.Endpoints</c>) which walks
/// <c>EndpointDataSource</c> and exposes the resolved
/// <c>RouteEndpoint.RoutePattern.RawText</c> on <c>EntityDiscoveryLinks</c>.
/// </para>
/// <para>
/// Endpoint metadata is the same mechanism ASP.NET Core's own OpenAPI generator
/// uses to associate routes with the data it needs (operation name, tags,
/// produces, auth). Treating "which entity does this list ?" the same way keeps
/// a single source of truth — ASP.NET's endpoint table — instead of a parallel
/// registry that would drift the moment an endpoint is mounted conditionally,
/// renamed, or removed.
/// </para>
/// <para>
/// Usage from any producer module:
/// </para>
/// <code>
/// group.MapGet("", ...)
///     .WithMetadata(new EntityEndpointMetadata(typeof(Party), EntityEndpointKind.List))
///     .WithName(...);
/// </code>
/// </remarks>
/// <param name="EntityType">
/// CLR type of the entity surfaced by this endpoint — matches
/// <c>EntityDefinitionDescriptor.EntityType</c>.
/// </param>
/// <param name="Kind">Which endpoint kind this route serves.</param>
public sealed record EntityEndpointMetadata(Type EntityType, EntityEndpointKind Kind);
