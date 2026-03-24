namespace Granit.Users;

/// <summary>
/// Identifies the type of actor performing the current operation.
/// Used to distinguish human users from external systems (API keys)
/// and internal system processes (background jobs, CRON, migrations).
/// </summary>
public enum ActorKind
{
    /// <summary>Human user authenticated via identity provider (Keycloak).</summary>
    User,

    /// <summary>External system authenticated via API key (partner, ERP, third-party).</summary>
    ExternalSystem,

    /// <summary>Internal system process (background job, CRON, migration, seed).</summary>
    System,
}
