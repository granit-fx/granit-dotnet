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

    /// <summary>Shell sub-workspaces.</summary>
    public const string System = "Granit.Framework.System";

    /// <summary><inheritdoc cref="System"/></summary>
    public const string Users = "Granit.Framework.Users";

    /// <summary><inheritdoc cref="System"/></summary>
    public const string Automation = "Granit.Framework.Automation";

    /// <summary><inheritdoc cref="System"/></summary>
    public const string Data = "Granit.Framework.Data";

    /// <summary><inheritdoc cref="System"/></summary>
    public const string Email = "Granit.Framework.Email";

    /// <summary><inheritdoc cref="System"/></summary>
    public const string Integrations = "Granit.Framework.Integrations";

    /// <summary><inheritdoc cref="System"/></summary>
    public const string Monitoring = "Granit.Framework.Monitoring";

    /// <summary><inheritdoc cref="System"/></summary>
    public const string Privacy = "Granit.Framework.Privacy";
}
