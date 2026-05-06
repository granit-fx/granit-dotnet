using Granit.Entities;
using Granit.Entities.Manifests;

namespace Granit.Entities.Internal;

/// <summary>
/// Default no-op <see cref="IManifestCustomizationApplier"/> registered by
/// <c>GranitEntitiesEndpointsModule</c>. Hosts that omit
/// <c>Granit.Entities.Customization.Endpoints</c> see the compiled manifest
/// unchanged — every field's <c>Provenance</c> is the implicit
/// <see cref="EntityProvenanceLayers.Compiled"/>.
/// </summary>
internal sealed class NullManifestCustomizationApplier : IManifestCustomizationApplier
{
    public Task<EntityManifestResponse> ApplyAsync(
        string entityName,
        EntityManifestResponse composedManifest,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(composedManifest);
}
