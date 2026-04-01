namespace Granit.QueryEngine.Endpoints.Options;

/// <summary>
/// Configuration options for query endpoints registered via
/// <see cref="QueryEndpointRouteBuilderExtensions.MapGranitQuery{TEntity}"/>.
/// </summary>
public sealed class QueryEndpointOptions
{
    /// <summary>
    /// The OpenAPI tag name for the query endpoints.
    /// Defaults to the entity type name.
    /// </summary>
    public string? TagName { get; set; }

    /// <summary>
    /// Authorization policy name to apply to all endpoints.
    /// When <c>null</c>, no policy is applied.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// Whether to allow anonymous access to the query endpoints.
    /// Defaults to <c>false</c> — endpoints require authentication unless
    /// an <see cref="AuthorizationPolicy"/> is configured.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// Whether to register the <c>GET /meta</c> metadata endpoint.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeMetaEndpoint { get; set; } = true;

    /// <summary>
    /// Whether to register the saved views CRUD endpoints.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeSavedViewEndpoints { get; set; } = true;
}
