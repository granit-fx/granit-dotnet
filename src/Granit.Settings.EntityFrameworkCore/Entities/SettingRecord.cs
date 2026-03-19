using Granit.Core.Domain;

namespace Granit.Settings.EntityFrameworkCore.Entities;

/// <summary>
/// Persistent setting value record stored in the <c>core_setting_records</c> table.
/// </summary>
/// <remarks>
/// <para>
/// Identified by the triple (<see cref="Name"/>, <see cref="ProviderName"/>, <see cref="ProviderKey"/>)
/// which maps to a unique combination of setting name and provider scope (Global, Tenant, User).
/// </para>
/// <para>
/// Audit fields (<c>CreatedAt</c>, <c>CreatedBy</c>, <c>ModifiedAt</c>, <c>ModifiedBy</c>) are
/// populated automatically by <c>AuditedEntityInterceptor</c> from <c>Granit.Persistence</c>,
/// satisfying the ISO 27001 3-year audit trail requirement.
/// </para>
/// </remarks>
public sealed class SettingRecord : AuditedEntity, IEmitEntityLifecycleEvents
{
    /// <summary>Setting name (e.g. <c>"Notifications.Email.Enabled"</c>). Max 256 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short provider identifier: <c>"G"</c> (Global), <c>"T"</c> (Tenant), <c>"U"</c> (User).
    /// Max 4 characters.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Provider scope key: <c>null</c> for Global, tenant ID for Tenant, user ID for User.
    /// Max 256 characters.
    /// </summary>
    public string? ProviderKey { get; set; }

    /// <summary>Setting value (plain text — encryption handled by the store layer). Nullable.</summary>
    public string? Value { get; set; }
}
