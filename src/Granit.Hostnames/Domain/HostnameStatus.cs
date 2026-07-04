namespace Granit.Hostnames.Domain;

/// <summary>
/// Lifecycle state of a <see cref="ManagedHostname"/>. The verification state machine
/// (Pending → Verifying → Active / Error) is driven by the DNS verifier; see the verification
/// feature. The host-routing read path only serves <see cref="Active"/> hostnames.
/// </summary>
public enum HostnameStatus
{
    /// <summary>Registered but not yet verified — no DNS challenge attempted.</summary>
    Pending,

    /// <summary>Verification in progress — DNS is being polled against the expected records.</summary>
    Verifying,

    /// <summary>Verified and serving — eligible for host-based routing.</summary>
    Active,

    /// <summary>Verification failed — misconfigured DNS or a conflicting record.</summary>
    Error,
}
