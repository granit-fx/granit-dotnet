namespace Granit.DataLookup.Descriptors;

/// <summary>
/// Canonical item returned by every lookup source — the shape the frontend consumes
/// directly without mapping.
/// </summary>
/// <param name="Value">
/// Opaque value to submit back (e.g. a <see cref="System.Guid"/>, an enum name, a code).
/// Serialized as JSON and round-tripped unchanged.
/// </param>
/// <param name="Label">
/// Human-readable label <b>already resolved in the caller's culture</b>. The server honors
/// the <c>Accept-Language</c> header and the ambient <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
/// before projecting to this field. The frontend never re-localizes the label.
/// </param>
/// <param name="Extra">
/// Optional additional attributes the frontend can render as secondary information
/// (e.g. <c>{ "active": true, "email": "x@y.z" }</c>). Kept intentionally untyped to
/// support domain-specific metadata without bloating the base contract.
/// </param>
public sealed record LookupItem(
    object Value,
    string Label,
    IReadOnlyDictionary<string, object?>? Extra = null);
