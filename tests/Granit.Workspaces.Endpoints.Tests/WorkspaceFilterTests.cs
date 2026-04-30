using Granit.Authorization;
using Granit.Workspaces;
using Granit.Workspaces.Endpoints.Dtos;
using Granit.Workspaces.Endpoints.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workspaces.Endpoints.Tests;

public sealed class WorkspaceFilterTests
{
    [Fact]
    public async Task FilterAsync_drops_workspace_when_user_lacks_RequiresPermission()
    {
        WorkspaceDescriptor admin = BuildWs("Admin", requiresPermission: "Admin.Read",
            sectionKey: "all", item: BuildEntityItem("X.Y"));
        WorkspaceDescriptor open = BuildWs("Open", item: BuildEntityItem("X.Z"));

        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        WorkspaceFilter filter = new(checker);
        WorkspaceTreeResponse tree = await filter.FilterAsync(
            [admin, open], includeShells: true, TestContext.Current.CancellationToken);

        tree.Workspaces.Select(w => w.Name).ShouldBe(["Open"]);
    }

    [Fact]
    public async Task FilterAsync_drops_item_user_lacks_permission_for()
    {
        WorkspaceDescriptor ws = BuildWs("Sales", sectionKey: "kpis",
            items:
            [
                BuildEntityItem("Sales.Order"),
                BuildEntityItem("Sales.Margin", requiresPermission: "Sales.Margin.Read"),
            ]);

        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        WorkspaceFilter filter = new(checker);
        WorkspaceTreeResponse tree = await filter.FilterAsync(
            [ws], includeShells: true, TestContext.Current.CancellationToken);

        tree.Workspaces.Single().Sections.Single().Items
            .Select(i => i.EntityName)
            .ShouldBe(["Sales.Order"]);
    }

    [Fact]
    public async Task FilterAsync_omits_shells_when_includeShells_is_false()
    {
        WorkspaceDescriptor shell = BuildWs("Granit.Framework.System", isShell: true,
            sectionKey: "tools", item: BuildEntityItem("X.Y"));
        WorkspaceDescriptor regular = BuildWs("Sales",
            item: BuildEntityItem("X.Z"));

        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        WorkspaceFilter filter = new(checker);
        WorkspaceTreeResponse tree = await filter.FilterAsync(
            [shell, regular], includeShells: false, TestContext.Current.CancellationToken);

        tree.Workspaces.Select(w => w.Name).ShouldBe(["Sales"]);
    }

    [Fact]
    public async Task FilterAsync_drops_workspace_when_every_section_is_filtered_out()
    {
        WorkspaceDescriptor ws = BuildWs("Empty", sectionKey: "secret",
            items:
            [
                BuildEntityItem("S.Vault", requiresPermission: "Secrets.Vault.Read"),
            ]);

        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        WorkspaceFilter filter = new(checker);
        WorkspaceTreeResponse tree = await filter.FilterAsync(
            [ws], includeShells: true, TestContext.Current.CancellationToken);

        tree.Workspaces.ShouldBeEmpty();
    }

    private static WorkspaceItemDescriptor BuildEntityItem(string entityName, string? requiresPermission = null) =>
        new(WorkspaceItemKind.Entity, 0, null, null,
            entityName, null, null, null, null, null, requiresPermission);

    private static WorkspaceDescriptor BuildWs(
        string name,
        string? requiresPermission = null,
        bool isShell = false,
        string sectionKey = "default",
        WorkspaceItemDescriptor? item = null,
        IReadOnlyList<WorkspaceItemDescriptor>? items = null) =>
        new()
        {
            Name = name,
            RequiresPermission = requiresPermission,
            IsShell = isShell,
            Sections = items is not null
                ? [new WorkspaceSectionDescriptor(sectionKey, null, 0, false, items)]
                : item is not null
                    ? [new WorkspaceSectionDescriptor(sectionKey, null, 0, false, [item])]
                    : [],
        };
}
