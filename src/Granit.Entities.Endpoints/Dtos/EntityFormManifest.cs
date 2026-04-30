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
/// <param name="Fields">Fields the user is allowed to see (already permission-filtered server-side).</param>
public sealed record EntityFormSectionManifest(
    string Key,
    string? LabelKey,
    int Order,
    bool CollapsedByDefault,
    IReadOnlyList<EntityFormFieldManifest> Fields);

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
