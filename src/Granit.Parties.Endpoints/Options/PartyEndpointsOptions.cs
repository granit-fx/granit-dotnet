namespace Granit.Parties.Endpoints.Options;

/// <summary>Configuration for the parties admin endpoints.</summary>
public sealed class PartyEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Parties:Endpoints";

    /// <summary>Route group prefix. Default: <c>"/parties"</c>.</summary>
    public string RoutePrefix { get; set; } = "/parties";

    /// <summary>OpenAPI tag name. Default: <c>"Parties"</c>.</summary>
    public string TagName { get; set; } = "Parties";
}
