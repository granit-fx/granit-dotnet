namespace Granit.Parties.Endpoints.Options;

/// <summary>Configuration for the parties admin endpoints.</summary>
public sealed class PartyEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Parties:Endpoints";

    /// <summary>Route group prefix. Default: <c>"/parties"</c>.</summary>
    public string RoutePrefix { get; set; } = "/parties";

    /// <summary>
    /// OpenAPI tag for the core Party CRUD + lifecycle + vCard endpoints.
    /// Default: <c>"Parties"</c>.
    /// </summary>
    public string TagName { get; set; } = "Parties";

    /// <summary>
    /// OpenAPI tag for the per-Party communication sub-collections — addresses,
    /// emails, phones, external provider mappings.
    /// Default: <c>"Parties - Contact Info"</c>.
    /// </summary>
    public string ContactInfoTagName { get; set; } = "Parties - Contact Info";

    /// <summary>
    /// OpenAPI tag for admin classification endpoints — roles, tax status, metadata,
    /// internal notes.
    /// Default: <c>"Parties - Classification"</c>.
    /// </summary>
    public string ClassificationTagName { get; set; } = "Parties - Classification";

    /// <summary>
    /// OpenAPI tag for the merge admin tool (preview + execute).
    /// Default: <c>"Parties - Merge"</c>.
    /// </summary>
    public string MergeTagName { get; set; } = "Parties - Merge";

    /// <summary>
    /// OpenAPI tag for the duplicate-candidates review inbox.
    /// Default: <c>"Parties - Duplicates"</c>.
    /// </summary>
    public string DuplicatesTagName { get; set; } = "Parties - Duplicates";
}
