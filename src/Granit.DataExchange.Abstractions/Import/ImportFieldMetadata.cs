namespace Granit.DataExchange.Import;

/// <summary>
/// Metadata about a target entity property, used for mapping suggestions.
/// Contains only schema information — never business data (GDPR/ISO 27001 safe).
/// </summary>
/// <param name="PropertyPath">Property path on the entity (e.g. <c>"Email"</c>, <c>"Lines.ProductName"</c>).</param>
/// <param name="ClrTypeName">CLR type name (e.g. <c>"String"</c>, <c>"DateTimeOffset"</c>).</param>
/// <param name="DisplayName">User-facing display name, or <c>null</c> if not configured.</param>
/// <param name="Description">Property description, or <c>null</c>.</param>
/// <param name="IsRequired">Whether the property is required for import.</param>
public sealed record ImportFieldMetadata(
    string PropertyPath,
    string ClrTypeName,
    string? DisplayName,
    string? Description,
    bool IsRequired);
