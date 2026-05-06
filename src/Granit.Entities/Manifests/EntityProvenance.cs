namespace Granit.Entities.Manifests;

/// <summary>
/// Per-field attribution token added by the manifest composer (ADR-053 §6).
/// The dev-mode field-inspector overlay in the React shell renders a small
/// badge per field that resolves to this token, so a developer can answer
/// "where does this field come from?" without grepping the codebase.
/// </summary>
/// <param name="Layer">
/// The source layer; one of <see cref="EntityProvenanceLayers.Compiled"/> or
/// <see cref="EntityProvenanceLayers.TenantCustomization"/>. (URL state, saved
/// view, and workspace preset layers — when they ship — also slot in here.)
/// </param>
/// <param name="OverrideId">
/// Identifier of the customization row that produced this provenance, when
/// applicable. <see langword="null"/> for the compiled layer.
/// </param>
public sealed record EntityProvenance(string Layer, Guid? OverrideId);

/// <summary>Stable wire identifiers for <see cref="EntityProvenance.Layer"/>.</summary>
public static class EntityProvenanceLayers
{
    /// <summary>Compiled defaults — the field comes straight from <c>EntityDefinition&lt;T&gt;</c>.</summary>
    public const string Compiled = "compiled";

    /// <summary>Layer 1 tenant customization (ADR-053).</summary>
    public const string TenantCustomization = "tenant-customization";
}
