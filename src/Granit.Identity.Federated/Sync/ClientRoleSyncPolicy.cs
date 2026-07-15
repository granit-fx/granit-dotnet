using Granit.Authorization;

namespace Granit.Identity.Federated.Sync;

/// <summary>
/// The category of an expected, skippable failure while fetching a client's roles from an
/// identity provider. The <see cref="ClientRoleSyncEngine"/> logs each category differently and
/// continues with the next tracked client; anything a policy does not classify rethrows.
/// </summary>
public enum ClientRoleFetchFault
{
    /// <summary>The tracked client id does not exist in the provider — skip it.</summary>
    ClientNotFound,

    /// <summary>The sync service account lacks the read permissions the provider requires.</summary>
    Forbidden,

    /// <summary>A transient transport/timeout failure — the next run is a natural retry.</summary>
    Transient,
}

/// <summary>
/// The per-provider slice of client-role sync: which clients to track, whether it is enabled, the
/// orphan policy, and how to classify provider-specific fetch exceptions. Everything else — the
/// idempotent upsert pass and the ADR-029 orphan handling — is shared in
/// <see cref="ClientRoleSyncEngine"/>. Each federated provider registers one implementation.
/// </summary>
public interface IClientRoleSyncPolicy
{
    /// <summary>Human-readable provider name used in log messages (e.g. <c>Keycloak</c>).</summary>
    string ProviderName { get; }

    /// <summary>Whether client-role sync is enabled for this provider.</summary>
    bool Enabled { get; }

    /// <summary>The client / app ids whose roles are projected into the role store.</summary>
    IReadOnlyList<string> TrackedClientIds { get; }

    /// <summary>How to handle store rows no longer returned by the provider (ADR-029).</summary>
    OrphanedRolePolicy OrphanedRolePolicy { get; }

    /// <summary>
    /// A short provider-specific remediation hint appended to the "access denied" log — e.g. the
    /// exact admin roles the service account needs. Empty when there is nothing useful to add.
    /// </summary>
    string ForbiddenRemediation { get; }

    /// <summary>
    /// Classifies an exception thrown while fetching a client's roles. Returns the fault category
    /// for an expected/skippable failure, or <c>null</c> to let the exception propagate.
    /// </summary>
    ClientRoleFetchFault? ClassifyFetchFault(Exception exception);
}
