using Granit.Localization;

namespace Granit.Authorization;

/// <summary>Defines a single permission that can be granted to a role.</summary>
/// <param name="Name">Unique permission name, e.g. <c>"Invoices.Delete"</c>.</param>
/// <param name="DisplayName">Optional localizable label for admin UIs.</param>
/// <param name="GroupName">Name of the group this permission belongs to.</param>
/// <param name="MultiTenancySide">
/// Tenant/host scope of the permission. Defaults to <see cref="MultiTenancySide.Both"/> for
/// backwards compatibility with existing permission declarations.
/// </param>
public sealed record PermissionDefinition(
    string Name,
    LocalizableString? DisplayName,
    string GroupName,
    MultiTenancySide MultiTenancySide = MultiTenancySide.Both);
