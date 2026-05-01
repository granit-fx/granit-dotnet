using Granit.Entities.Visibility;

namespace Granit.Entities.Endpoints.Dtos;

/// <summary>One form variant exposed in the manifest.</summary>
/// <param name="Name">Variant name, unique per entity (e.g. <c>"default"</c>).</param>
/// <param name="Customizable">When <see langword="true"/>, tenant admins may reorder/regroup/hide fields (Tier B Layer 1).</param>
/// <param name="Sections">Sections in declaration order.</param>
public sealed record EntityFormManifest(
    string Name,
    bool Customizable,
    IReadOnlyList<EntityFormSectionManifest> Sections);

/// <summary>One form section.</summary>
/// <param name="Key">Stable section key (e.g. <c>"identity"</c>).</param>
/// <param name="LabelKey">i18n key for the section header.</param>
/// <param name="Order">Display order (lower first).</param>
/// <param name="CollapsedByDefault">Whether the section starts collapsed in the renderer.</param>
/// <param name="Fields">Scalar fields (empty when <paramref name="OwnedCollection"/> is non-null).</param>
/// <param name="OwnedCollection">Non-null when the section renders as a list of owned items.</param>
public sealed record EntityFormSectionManifest(
    string Key,
    string? LabelKey,
    int Order,
    bool CollapsedByDefault,
    IReadOnlyList<EntityFormFieldManifest> Fields,
    EntityFormOwnedCollectionManifest? OwnedCollection = null);

/// <summary>One form field.</summary>
/// <param name="PropertyName">PascalCase property name on the entity.</param>
/// <param name="ClrTypeName">Short CLR type name — drives the renderer when <see cref="Widget"/> doesn't override it.</param>
/// <param name="Widget">Widget identifier from the standard catalog or <c>custom:</c> namespace (ADR-041).</param>
/// <param name="Config">Opaque widget-specific configuration, or <see langword="null"/>.</param>
/// <param name="LabelKey">i18n key for the field label.</param>
/// <param name="HelpKey">i18n key for the help text under the field.</param>
/// <param name="Order">Display order within the section.</param>
/// <param name="ReadOnly">Read-only in the form context.</param>
/// <param name="VisibleIf">Closed-DSL conditional-visibility rule (ADR-040), or <see langword="null"/>.</param>
public sealed record EntityFormFieldManifest(
    string PropertyName,
    string ClrTypeName,
    string Widget,
    IReadOnlyDictionary<string, object?>? Config,
    string? LabelKey,
    string? HelpKey,
    int Order,
    bool ReadOnly,
    VisibilityCondition? VisibleIf);

/// <summary>An owned-collection sub-section (rendered as a list of items per ADR-040).</summary>
/// <param name="PropertyName">Owner-side property exposing the collection (e.g. <c>"Addresses"</c>).</param>
/// <param name="ItemTypeName">Short CLR type name of the item.</param>
/// <param name="ItemFields">Per-item form schema, in declaration order (already permission-filtered).</param>
/// <param name="ItemDisplayProperty">Optional item property used as the collapsed-row headline.</param>
/// <param name="MaxRendered">Optional renderer hint capping inline expansion.</param>
public sealed record EntityFormOwnedCollectionManifest(
    string PropertyName,
    string ItemTypeName,
    IReadOnlyList<EntityFormFieldManifest> ItemFields,
    string? ItemDisplayProperty,
    int? MaxRendered);
