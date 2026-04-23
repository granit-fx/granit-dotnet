namespace Granit.Authorization;

/// <summary>
/// Controls what the client-role sync pipeline does when a
/// <see cref="Domain.RoleMetadata"/> row no longer has a counterpart in the upstream
/// identity provider (Keycloak client role deleted, Entra App Role removed, Cognito
/// group renamed out of the tracked prefix, etc.).
/// </summary>
/// <remarks>
/// Phase 2 shipped with the implicit <see cref="KeepAndLog"/> behaviour. Phase 3
/// (<c>ADR-029</c>) adds this enum so operators can pick a stricter stance without
/// bolting custom scripts onto the sync.
/// </remarks>
public enum OrphanedRolePolicy
{
    /// <summary>
    /// Default — the sync leaves the <see cref="Domain.RoleMetadata"/> row intact and
    /// logs the drift at <c>Information</c> level. No state change on the aggregate,
    /// no integration event fired. Grants keep resolving normally.
    /// </summary>
    /// <remarks>
    /// Zero-config upgrade target: existing Phase 2 consumers stay on this policy
    /// unless they explicitly opt in to a stricter one.
    /// </remarks>
    KeepAndLog = 0,

    /// <summary>
    /// The sync flips <c>IsOrphaned = true</c> and stamps <c>OrphanedAt</c> on any
    /// row whose name is no longer returned by the provider, then saves. The
    /// <see cref="Domain.Events.RoleOrphanedEvent"/> domain event and the
    /// <see cref="Events.RoleOrphanedEto"/> integration event fire. Permission grants
    /// keep resolving because the row is preserved; an admin reviews and either
    /// restores (admin re-adds the role upstream, the next sync clears the flag) or
    /// hard-deletes (via the admin endpoint, tracked separately).
    /// </summary>
    SoftDelete = 1,

    /// <summary>
    /// The sync removes the <see cref="Domain.RoleMetadata"/> row entirely. The
    /// cascading FK on <c>PermissionGrant</c> means every grant referencing the
    /// deleted role is also removed, silently stripping permissions from active
    /// users. Only use this policy when grants are reconciled from an external source
    /// of truth (IaC rewrites, SSO group mapping) or during a migration that
    /// explicitly accepts the blast radius.
    /// </summary>
    HardDelete = 2,
}
