namespace Granit.Workspaces.Framework.Internal;

/// <summary>
/// The 8 framework shell sub-workspaces. Each is declared by name only and
/// flagged as a shell — populated at boot through
/// <see cref="IWorkspaceContributor"/> implementations. Empty shells
/// auto-filter from the rendered tree (handled by the workspace composer).
/// </summary>
internal sealed class SystemShellWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => FrameworkWorkspaceNames.System;
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.System").Icon("server").Order(0).Shell();
}

internal sealed class UsersShellWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => FrameworkWorkspaceNames.Users;
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.Users").Icon("users").Order(1).Shell();
}

internal sealed class AutomationShellWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => FrameworkWorkspaceNames.Automation;
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.Automation").Icon("zap").Order(2).Shell();
}

internal sealed class DataShellWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => FrameworkWorkspaceNames.Data;
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.Data").Icon("database").Order(3).Shell();
}

internal sealed class EmailShellWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => FrameworkWorkspaceNames.Email;
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.Email").Icon("mail").Order(4).Shell();
}

internal sealed class IntegrationsShellWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => FrameworkWorkspaceNames.Integrations;
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.Integrations").Icon("plug").Order(5).Shell();
}

internal sealed class MonitoringShellWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => FrameworkWorkspaceNames.Monitoring;
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.Monitoring").Icon("activity").Order(6).Shell();
}

internal sealed class PrivacyShellWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => FrameworkWorkspaceNames.Privacy;
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.Privacy").Icon("shield").Order(7).Shell();
}
