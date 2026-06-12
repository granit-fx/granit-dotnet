namespace Granit.Http.ApiDocumentation.Internal;

/// <summary>
/// Marker metadata attached to the Scalar UI endpoint by
/// <c>UseGranitApiDocumentation</c>. Inspected by
/// <see cref="ScalarCspContributor"/> via
/// <c>context.GetEndpoint()?.Metadata.GetMetadata&lt;ScalarApiReferenceMetadata&gt;()</c>
/// to scope the CSP relaxation to the Scalar route only.
/// </summary>
internal sealed class ScalarApiReferenceMetadata
{
    /// <summary>Shared stateless marker instance.</summary>
    public static ScalarApiReferenceMetadata Instance { get; } = new();
}
