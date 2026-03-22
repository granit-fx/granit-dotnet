using Granit.ReferenceData.Options;

namespace Granit.ReferenceData;

/// <summary>
/// Immutable metadata for a dynamically registered reference data type.
/// Created by the <c>AddReferenceData&lt;TDbContext&gt;()</c> fluent API.
/// </summary>
/// <param name="TypeName">
/// The logical name of the reference data type (e.g., <c>"Countries"</c>).
/// Used as the DI keyed service key and the route segment.
/// </param>
/// <param name="Options">The extension options declared via the fluent builder.</param>
public sealed record ReferenceDataTypeRegistration(
    string TypeName,
    ReferenceDataExtensionOptions Options);
