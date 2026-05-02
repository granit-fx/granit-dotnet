using Granit.Entities.Endpoints.Dtos;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Layer 4 of the manifest resolution hierarchy (ADR-053 §5) — the per-tenant
/// customization step. The composer (steps 5 → compiled defaults) runs first;
/// this applier mutates the resulting payload to apply the tenant's
/// <c>EntityCustomization</c> deltas (reorder / regroup / hide) and tag
/// affected fields with <see cref="EntityProvenance"/>.
/// </summary>
/// <remarks>
/// <para>
/// The default registration is the <see cref="NullManifestCustomizationApplier"/>
/// no-op — hosts that do not load <c>Granit.Entities.Customization.Endpoints</c>
/// see the compiled manifest unchanged. The customization endpoints package
/// replaces the registration via <c>services.Replace</c> with the real
/// implementation.
/// </para>
/// <para>
/// Applies INSIDE the manifest cache miss path so the customized payload is
/// the cached value — re-applying on every read would defeat the cache. PUT /
/// DELETE on the customization endpoint evict the per-entity cache via the
/// <see cref="EntityCacheKey.EvictionTagForManifest"/> tag.
/// </para>
/// </remarks>
public interface IManifestCustomizationApplier
{
    /// <summary>
    /// Returns a customized copy of <paramref name="composedManifest"/> with
    /// the active per-tenant deltas applied, or the input unchanged when no
    /// customization exists.
    /// </summary>
    Task<EntityManifestResponse> ApplyAsync(
        string entityName,
        EntityManifestResponse composedManifest,
        CancellationToken cancellationToken = default);
}
