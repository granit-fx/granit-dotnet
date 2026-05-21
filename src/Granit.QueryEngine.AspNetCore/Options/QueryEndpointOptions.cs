namespace Granit.QueryEngine.AspNetCore.Options;

/// <summary>
/// Configuration options for query endpoints registered via
/// <c>QueryEndpointRouteBuilderExtensions.MapGranitQuery{TEntity}</c>.
/// </summary>
public sealed class QueryEndpointOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "QueryEngine:Endpoint";

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
    /// Whether to register the list endpoint (<c>GET /</c>).
    /// Defaults to <c>true</c>. Set to <c>false</c> when the host provides its own
    /// bespoke list endpoint on the same route group and only wants <c>/meta</c> from
    /// the query engine (avoids route collision on <c>GET /</c>).
    /// </summary>
    public bool IncludeListEndpoint { get; set; } = true;
}
