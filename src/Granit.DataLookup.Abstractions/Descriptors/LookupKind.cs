namespace Granit.DataLookup.Descriptors;

/// <summary>
/// Kind of backing implementation for a <see cref="LookupDescriptor"/>.
/// </summary>
/// <remarks>
/// The kind is informational: the frontend and the registry both dispatch on
/// <see cref="LookupDescriptor.Name"/> or <see cref="LookupDescriptor.Endpoint"/>.
/// The kind lets the UI render a relevant affordance (e.g., a static dropdown for
/// <see cref="Enum"/>, a typeahead combobox for <see cref="QueryEngine"/>).
/// </remarks>
public enum LookupKind
{
    /// <summary>
    /// Backed by a <c>Granit.QueryEngine</c> definition. Full text search, pagination,
    /// tenant isolation and authorization are inherited from the query endpoint.
    /// </summary>
    QueryEngine = 0,

    /// <summary>
    /// Backed by a custom lightweight HTTP endpoint that returns the canonical
    /// <see cref="LookupResult"/> shape but is not a QueryEngine endpoint.
    /// </summary>
    Simple = 1,

    /// <summary>
    /// Backed by a <c>Granit.ReferenceData</c> set. Labels are resolved in the user's
    /// culture from the <c>Label{Culture}</c> columns of the reference-data entity.
    /// </summary>
    ReferenceData = 2,

    /// <summary>
    /// Backed by a CLR <c>enum</c> type. Values are the enum names; labels are resolved
    /// via <c>Granit.Localization</c> keys <c>Enum:{TypeName}.{Value}</c>.
    /// </summary>
    Enum = 3,
}
