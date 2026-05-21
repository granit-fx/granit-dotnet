using System.ComponentModel.DataAnnotations;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Naming convention for per-tenant PostgreSQL schemas.
/// </summary>
public enum TenantSchemaNamingConvention
{
    /// <summary>
    /// Schema name derived from the tenant identifier (GUID without hyphens).
    /// Example: <c>tenant_3fa85f6456954b5ab7d9c4f11f0b7f5e</c>
    /// </summary>
    TenantId,

    /// <summary>
    /// Schema name derived from the tenant display name (lower-case, URL-safe).
    /// Requires <see cref="ITenantSchemaProvider"/> to resolve the name from the identifier.
    /// Example: <c>tenant_acme</c>
    /// </summary>
    TenantName,

    /// <summary>
    /// Schema name provided by a custom <see cref="ITenantSchemaProvider"/> implementation.
    /// </summary>
    Custom,
}

/// <summary>
/// Options for the per-tenant schema isolation strategy.
/// Bound from the <c>"MultiTenancy:TenantSchema"</c> configuration section.
/// </summary>
public sealed class TenantSchemaOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "MultiTenancy:TenantSchema";

    /// <summary>
    /// Naming convention used to derive the PostgreSQL schema name from the tenant context.
    /// Defaults to <see cref="TenantSchemaNamingConvention.TenantId"/>.
    /// </summary>
    public TenantSchemaNamingConvention NamingConvention { get; set; } =
        TenantSchemaNamingConvention.TenantId;

    /// <summary>
    /// Prefix prepended to every schema name.
    /// Must contain only lowercase letters, digits, and underscores (PostgreSQL safe identifier).
    /// Defaults to <c>"tenant_"</c>.
    /// </summary>
    [Required]
    [RegularExpression(@"^[a-z][a-z0-9_]*$", ErrorMessage = "Prefix must be a safe PostgreSQL identifier: lowercase letters, digits, and underscores, starting with a letter.")]
    [StringLength(20, MinimumLength = 1)]
    public string Prefix { get; set; } = "tenant_";
}
