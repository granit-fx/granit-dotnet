namespace Granit.Entities.Endpoints.Dtos;

/// <summary>Identity facet of the per-entity manifest.</summary>
/// <param name="Name">Wire identifier.</param>
/// <param name="EntityClrType">CLR full name of the entity (debugging aid; not security-sensitive — already public via OpenAPI schemas).</param>
/// <param name="DisplayKey">i18n key for the singular display name.</param>
/// <param name="Icon">Icon from the standard catalog, or <see langword="null"/>.</param>
/// <param name="PermissionGroup">Permission-group prefix (e.g. <c>"Parties.Parties"</c>).</param>
/// <param name="DisplayProperty">Property used to label references (e.g. <c>"Number"</c>), or <see langword="null"/>.</param>
/// <param name="SubtitleProperty">Optional secondary property displayed alongside <paramref name="DisplayProperty"/> for context (e.g. <c>"PartyName"</c> on an Invoice → renderer shows "INV-001 — Acme Corp"). <see langword="null"/> when the entity declares no subtitle.</param>
public sealed record EntityIdentitySection(
    string Name,
    string EntityClrType,
    string? DisplayKey,
    string? Icon,
    string? PermissionGroup,
    string? DisplayProperty,
    string? SubtitleProperty);
