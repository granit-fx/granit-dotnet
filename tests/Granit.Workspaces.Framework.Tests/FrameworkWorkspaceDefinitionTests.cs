using System.Reflection;
using Granit.Workspaces;
using Granit.Workspaces.Framework;
using Shouldly;
using Xunit;

namespace Granit.Workspaces.Framework.Tests;

public sealed class FrameworkWorkspaceDefinitionTests
{
    [Fact]
    public void Framework_root_is_gated_by_admin_permission()
    {
        WorkspaceDescriptor d = new FrameworkWorkspaceDefinition().Descriptor;

        d.Name.ShouldBe(FrameworkWorkspaceNames.Framework);
        d.RequiresPermission.ShouldBe(FrameworkWorkspaceNames.FrameworkReadPermission);
        d.IsShell.ShouldBeFalse();
    }

    [Fact]
    public void Framework_root_lists_the_eight_shell_sub_workspaces_in_canonical_order()
    {
        WorkspaceDescriptor d = new FrameworkWorkspaceDefinition().Descriptor;

        WorkspaceSectionDescriptor section = d.Sections.Single();
        section.Items.Select(i => i.SubWorkspaceName).ShouldBe(
        [
            FrameworkWorkspaceNames.System,
            FrameworkWorkspaceNames.Users,
            FrameworkWorkspaceNames.Automation,
            FrameworkWorkspaceNames.Data,
            FrameworkWorkspaceNames.Email,
            FrameworkWorkspaceNames.Integrations,
            FrameworkWorkspaceNames.Monitoring,
            FrameworkWorkspaceNames.Privacy,
        ]);
    }

    [Fact]
    public void All_eight_shell_definitions_are_marked_as_shells()
    {
        // Reflection-based — the 8 shell types are internal so we discover them
        // via the assembly to avoid hand-listing each one (which the
        // localisation-completeness test catches anyway).
        Assembly assembly = typeof(FrameworkWorkspaceDefinition).Assembly;
        IReadOnlyList<WorkspaceDefinition> shells = [..
            assembly.GetTypes()
                .Where(t => !t.IsAbstract
                    && typeof(WorkspaceDefinition).IsAssignableFrom(t)
                    && t.Name.EndsWith("ShellWorkspaceDefinition", StringComparison.Ordinal))
                .Select(t => (WorkspaceDefinition)Activator.CreateInstance(t)!)];

        shells.Count.ShouldBe(8);
        shells.ShouldAllBe(d => d.Descriptor.IsShell);
    }
}
