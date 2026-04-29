using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Builds an <see cref="IEdmModel"/> from the registered
/// <see cref="ODataEntitySetDescriptor"/>s. Convention-based — all public
/// CLR properties on the entity type appear on the EDM EntityType. Column
/// pruning beyond that (e.g. masking <c>QueryDefinition.AllowedColumns</c>)
/// is a follow-up; v1 mirrors the shape of the entity directly.
/// </summary>
internal static class ODataEdmModelBuilder
{
    /// <summary>
    /// Builds the EDM model for the supplied descriptors.
    /// </summary>
    /// <param name="descriptors">EntitySet descriptors registered via the fluent options.</param>
    /// <returns>The built <see cref="IEdmModel"/>, ready to wire into <c>WithODataModel</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="descriptors"/> is empty.</exception>
    public static IEdmModel Build(IReadOnlyList<ODataEntitySetDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        if (descriptors.Count == 0)
        {
            throw new ArgumentException(
                "At least one EntitySet must be registered before building the EDM model.",
                nameof(descriptors));
        }

        ODataConventionModelBuilder builder = new();

        foreach (ODataEntitySetDescriptor descriptor in descriptors)
        {
            // AddEntitySet wires both the EntityType (via reflection on the
            // entity's public properties) AND the EntitySet declaration in a
            // single call — the convention model builder picks up keys
            // from properties named "Id" / "{Type}Id".
            builder.AddEntitySet(
                descriptor.EntitySetName,
                builder.AddEntityType(descriptor.EntityType));
        }

        return builder.GetEdmModel();
    }
}
