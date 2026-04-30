namespace Granit.Workspaces.Framework;

/// <summary>
/// Root framework workspace — a single section listing the 8 shell
/// sub-workspaces populated by other modules through
/// <see cref="IWorkspaceContributor"/>. Hidden from non-admin users via the
/// <c>Workspace.Granit.Framework.Read</c> permission gate.
/// </summary>
public sealed class FrameworkWorkspaceDefinition : WorkspaceDefinition
{
    /// <inheritdoc/>
    public override string Name => FrameworkWorkspaceNames.Framework;

    /// <inheritdoc/>
    protected override void Configure(WorkspaceBuilder builder) =>
        builder.DisplayKey("WorkspacesFramework:Workspace.Framework")
            .Icon("settings")
            .Order(1000)
            .RequiresPermission(FrameworkWorkspaceNames.FrameworkReadPermission)
            .Section("framework", s => s
                .DisplayKey("WorkspacesFramework:Section.Framework")
                .SubWorkspace(FrameworkWorkspaceNames.System, i => i
                    .DisplayKey("WorkspacesFramework:Workspace.System").Icon("server").Order(0))
                .SubWorkspace(FrameworkWorkspaceNames.Users, i => i
                    .DisplayKey("WorkspacesFramework:Workspace.Users").Icon("users").Order(1))
                .SubWorkspace(FrameworkWorkspaceNames.Automation, i => i
                    .DisplayKey("WorkspacesFramework:Workspace.Automation").Icon("zap").Order(2))
                .SubWorkspace(FrameworkWorkspaceNames.Data, i => i
                    .DisplayKey("WorkspacesFramework:Workspace.Data").Icon("database").Order(3))
                .SubWorkspace(FrameworkWorkspaceNames.Email, i => i
                    .DisplayKey("WorkspacesFramework:Workspace.Email").Icon("mail").Order(4))
                .SubWorkspace(FrameworkWorkspaceNames.Integrations, i => i
                    .DisplayKey("WorkspacesFramework:Workspace.Integrations").Icon("plug").Order(5))
                .SubWorkspace(FrameworkWorkspaceNames.Monitoring, i => i
                    .DisplayKey("WorkspacesFramework:Workspace.Monitoring").Icon("activity").Order(6))
                .SubWorkspace(FrameworkWorkspaceNames.Privacy, i => i
                    .DisplayKey("WorkspacesFramework:Workspace.Privacy").Icon("shield").Order(7)));
}
