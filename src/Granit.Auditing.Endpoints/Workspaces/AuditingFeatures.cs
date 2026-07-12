namespace Granit.Auditing.Endpoints.Workspaces;

/// <summary>Feature name constants for the auditing module (per ADR-057).</summary>
public static class AuditingFeatures
{
    /// <summary>Audit log browser — paired with the <c>auditing/audit-entries</c> endpoints (SPA route name <c>auditing.entries</c>).</summary>
    public const string Entries = "auditing.entries";
}
