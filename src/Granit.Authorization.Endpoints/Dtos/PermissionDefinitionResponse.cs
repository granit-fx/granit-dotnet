using Granit.MultiTenancy;

namespace Granit.Authorization.Endpoints.Dtos;

/// <summary>
/// Response representing a single permission definition.
/// </summary>
/// <param name="Name">Stable permission identifier (e.g. <c>"Invoices.Invoices.Read"</c>).</param>
/// <param name="DisplayName">Localized display name for admin UIs, or <see langword="null"/> if not set.</param>
/// <param name="MultiTenancySides">
/// Declared scope of the permission: <c>"Host"</c>, <c>"Tenant"</c>, or <c>"Both"</c>.
/// Consumed by admin UIs to filter grantable permissions by the current scope and to render
/// a side badge next to each permission. Serialized as the enum name.
/// </param>
public sealed record PermissionDefinitionResponse(
    string Name,
    string? DisplayName,
    MultiTenancySides MultiTenancySides);
