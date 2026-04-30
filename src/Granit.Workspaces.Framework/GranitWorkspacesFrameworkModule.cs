using Granit.Authorization;
using Granit.Modularity;
using Granit.Workspaces.Extensions;
using Granit.Workspaces.Framework.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Workspaces.Framework;

/// <summary>
/// Granit module for the framework navigation shell — registers the
/// <see cref="FrameworkWorkspaceDefinition"/> root and the eight shell
/// sub-workspaces (System / Users / Automation / Data / Email / Integrations
/// / Monitoring / Privacy). Other modules graft their admin entries onto a
/// shell via <see cref="IWorkspaceContributor"/>.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitWorkspacesFrameworkModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        IServiceCollection s = context.Services;
        s.AddWorkspaceDefinition<FrameworkWorkspaceDefinition>();
        s.AddWorkspaceDefinition<SystemShellWorkspaceDefinition>();
        s.AddWorkspaceDefinition<UsersShellWorkspaceDefinition>();
        s.AddWorkspaceDefinition<AutomationShellWorkspaceDefinition>();
        s.AddWorkspaceDefinition<DataShellWorkspaceDefinition>();
        s.AddWorkspaceDefinition<EmailShellWorkspaceDefinition>();
        s.AddWorkspaceDefinition<IntegrationsShellWorkspaceDefinition>();
        s.AddWorkspaceDefinition<MonitoringShellWorkspaceDefinition>();
        s.AddWorkspaceDefinition<PrivacyShellWorkspaceDefinition>();
    }
}
