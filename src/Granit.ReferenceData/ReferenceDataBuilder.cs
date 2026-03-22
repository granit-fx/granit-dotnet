using Granit.ReferenceData.Options;

namespace Granit.ReferenceData;

/// <summary>
/// Fluent builder for registering multiple reference data types in one call.
/// </summary>
/// <remarks>
/// <para>
/// Usage:
/// <code>
/// services.AddReferenceData&lt;AppDbContext&gt;(rd =&gt;
/// {
///     rd.Add("Countries", opts =&gt; opts
///         .Table("ref_countries")
///         .MapProperty&lt;string&gt;("Alpha3Code", maxLength: 3, isFilterable: true));
///
///     rd.Add("DocumentTypes", opts =&gt; opts.Table("ref_document_types"));
/// });
/// </code>
/// </para>
/// </remarks>
public sealed class ReferenceDataBuilder
{
    internal List<ReferenceDataTypeRegistration> Registrations { get; } = [];

    /// <summary>
    /// Adds a reference data type with a logical name and optional configuration.
    /// </summary>
    /// <param name="typeName">
    /// The logical name (e.g., <c>"Countries"</c>). Used as the keyed service key
    /// and route segment.
    /// </param>
    /// <param name="configure">Optional fluent configuration for the type.</param>
    /// <returns>This builder for chaining.</returns>
    public ReferenceDataBuilder Add(string typeName, Action<ReferenceDataExtensionOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        ReferenceDataExtensionOptions options = new();
        configure?.Invoke(options);

        Registrations.Add(new ReferenceDataTypeRegistration(typeName, options));
        return this;
    }
}
