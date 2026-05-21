namespace Granit.Authentication.ApiKeys.Endpoints.Options;

/// <summary>
/// Configuration options for API key management endpoints.
/// </summary>
public sealed class ApiKeysEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Authentication:ApiKeys:Endpoints";

    /// <summary>Route prefix for API key endpoints. Default: <c>authentication</c>.</summary>
    public string RoutePrefix { get; set; } = "authentication";

    /// <summary>OpenAPI tag name. Default: <c>API Keys</c>.</summary>
    public string TagName { get; set; } = "API Keys";

    /// <summary>Allowed environments for key creation. Default: <c>live</c>, <c>test</c>, <c>dev</c>.</summary>
    public IReadOnlyList<string> AllowedEnvironments { get; set; } = ["live", "test", "dev"];
}
