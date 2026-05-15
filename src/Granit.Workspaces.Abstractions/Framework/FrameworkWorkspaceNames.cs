namespace Granit.Workspaces.Framework;

/// <summary>
/// Wire identifiers + permission strings used by the framework workspace
/// surface. Centralised here so cross-module contributors and tests can
/// reference them without typo'ing the wire form. The matching
/// <c>WorkspaceDefinition</c> implementations live in the
/// <c>Granit.Workspaces.Framework</c> package.
/// </summary>
public static class FrameworkWorkspaceNames
{
    /// <summary>Root framework workspace.</summary>
    public const string Framework = "Granit.Framework";

    /// <summary>Permission required to enter the framework workspace.</summary>
    public const string FrameworkReadPermission = "Workspace.Granit.Framework.Read";

    /// <summary>Identity, authorization, sessions, API keys, OIDC.</summary>
    public const string IdentityAccess = "Granit.Framework.IdentityAccess";

    /// <summary>Tenants, settings, features, localization overrides.</summary>
    public const string Platform = "Granit.Framework.Platform";

    /// <summary>Entities, views, taxonomy, workspaces — meta-model admin.</summary>
    public const string Customization = "Granit.Framework.Customization";

    /// <summary>Templating and notifications.</summary>
    public const string Communication = "Granit.Framework.Communication";

    /// <summary>Blob storage and data exchange (imports/exports).</summary>
    public const string Storage = "Granit.Framework.Storage";

    /// <summary>Background jobs, scheduling, workflow.</summary>
    public const string Automation = "Granit.Framework.Automation";

    /// <summary>Outbound integrations (webhooks).</summary>
    public const string Integrations = "Granit.Framework.Integrations";

    /// <summary>Analytics metric definitions and dashboards.</summary>
    public const string Insights = "Granit.Framework.Insights";

    /// <summary>AI workspaces and usage.</summary>
    public const string AI = "Granit.Framework.AI";

    /// <summary>Auditing, timeline, diagnostics.</summary>
    public const string Observability = "Granit.Framework.Observability";

    /// <summary>Privacy (GDPR) and regulatory compliance.</summary>
    public const string Compliance = "Granit.Framework.Compliance";
}
