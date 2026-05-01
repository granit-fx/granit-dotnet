namespace Granit.Identity.EntityFrameworkCore;

/// <summary>
/// Schema- and table-naming constants for the Identity EF Core store.
/// Pinned in one place so consuming apps' migrations and any custom
/// projection layers stay aligned with the framework's choice.
/// </summary>
public static class GranitIdentityDbProperties
{
    /// <summary>
    /// Table-name prefix applied to every Identity-owned table. Mirrors the
    /// per-module prefix convention (<c>granit_</c>, <c>granit_apikeys_</c>,
    /// etc.).
    /// </summary>
    public const string DbTablePrefix = "granit_identity_";

    /// <summary>
    /// PostgreSQL schema for the Identity tables, or <see langword="null"/>
    /// to use the default schema. <see langword="null"/> means the host
    /// decides (single-schema deployments simply don't set one).
    /// </summary>
    public const string? DbSchema = null;
}
