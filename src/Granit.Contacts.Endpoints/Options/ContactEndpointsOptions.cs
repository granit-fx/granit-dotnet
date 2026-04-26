namespace Granit.Contacts.Endpoints.Options;

/// <summary>Configuration for the contacts admin endpoints.</summary>
public sealed class ContactEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Contacts:Endpoints";

    /// <summary>Route group prefix. Default: <c>"/contacts"</c>.</summary>
    public string RoutePrefix { get; set; } = "/contacts";

    /// <summary>OpenAPI tag name. Default: <c>"Contacts"</c>.</summary>
    public string TagName { get; set; } = "Contacts";
}
