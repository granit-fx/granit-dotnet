using Granit.Workspaces;
using Granit.Workspaces.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Workspaces.Tests;

public sealed class WorkspaceTreeComposerTests
{
    [Fact]
    public void Compose_returns_descriptors_sorted_by_order_then_name()
    {
        SimpleDef a = new("Granit.A", order: 10);
        SimpleDef b = new("Granit.B", order: 5);
        SimpleDef c = new("Granit.C", order: 5);

        IReadOnlyList<WorkspaceDescriptor> all = WorkspaceTreeComposer.Compose(
            [a, b, c], contributors: [], NullLogger.Instance);

        all.Select(w => w.Name).ShouldBe(["Granit.B", "Granit.C", "Granit.A"]);
    }

    [Fact]
    public void Compose_drops_empty_shells()
    {
        SimpleDef shell = new("Granit.Framework.System", order: 0, shell: true);
        SimpleDef populated = new("Granit.Framework.Data", order: 1, shell: true,
            sectionKey: "tools", itemEntity: "X.Entity");

        IReadOnlyList<WorkspaceDescriptor> all = WorkspaceTreeComposer.Compose(
            [shell, populated], contributors: [], NullLogger.Instance);

        all.Select(w => w.Name).ShouldBe(["Granit.Framework.Data"]);
    }

    [Fact]
    public void Compose_throws_on_duplicate_name()
    {
        SimpleDef one = new("Granit.A", order: 0);
        SimpleDef two = new("Granit.A", order: 1);

        Should.Throw<InvalidOperationException>(() =>
            WorkspaceTreeComposer.Compose([one, two], contributors: [], NullLogger.Instance));
    }

    [Fact]
    public void Compose_merges_contributions_into_target_workspace()
    {
        SimpleDef target = new("Granit.Framework.Data", order: 0, shell: true);
        FakeContributor contributor = new("Granit.Framework.Data", "blob-storage", "BlobStorage.Blob");

        IReadOnlyList<WorkspaceDescriptor> all = WorkspaceTreeComposer.Compose(
            [target], [contributor], NullLogger.Instance);

        WorkspaceDescriptor data = all.Single();
        data.Sections.ShouldHaveSingleItem();
        data.Sections.Single().Items.ShouldHaveSingleItem();
        data.Sections.Single().Items.Single().EntityName.ShouldBe("BlobStorage.Blob");
    }

    [Fact]
    public void Compose_drops_contribution_targeting_unknown_workspace()
    {
        SimpleDef target = new("Granit.Framework.Data", order: 0,
            sectionKey: "default", itemEntity: "X.Y");
        FakeContributor stray = new("Granit.Framework.DoesNotExist", "x", "Y.Y");

        IReadOnlyList<WorkspaceDescriptor> all = WorkspaceTreeComposer.Compose(
            [target], [stray], NullLogger.Instance);

        all.Single().Sections.Single(s => s.Key == "default").Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Compose_throws_when_depth_exceeds_4()
    {
        // a -> b -> c -> d -> e (5 levels — over the limit)
        SimpleDef a = new("A", subWorkspace: "B");
        SimpleDef b = new("B", subWorkspace: "C");
        SimpleDef c = new("C", subWorkspace: "D");
        SimpleDef d = new("D", subWorkspace: "E");
        SimpleDef e = new("E");

        Should.Throw<InvalidOperationException>(() =>
            WorkspaceTreeComposer.Compose([a, b, c, d, e], contributors: [], NullLogger.Instance))
            .Message.ShouldContain("maximum depth");
    }

    [Fact]
    public void Compose_throws_on_cycle()
    {
        SimpleDef a = new("A", subWorkspace: "B");
        SimpleDef b = new("B", subWorkspace: "A");

        Should.Throw<InvalidOperationException>(() =>
            WorkspaceTreeComposer.Compose([a, b], contributors: [], NullLogger.Instance));
    }

    private sealed class SimpleDef : IWorkspaceDescriptor
    {
        private readonly WorkspaceDescriptor _descriptor;

        public SimpleDef(
            string name,
            int order = 0,
            bool shell = false,
            string? sectionKey = null,
            string? itemEntity = null,
            string? subWorkspace = null)
        {
            Name = name;
            List<WorkspaceItemDescriptor> items = [];
            if (itemEntity is not null)
            {
                items.Add(new WorkspaceItemDescriptor(
                    WorkspaceItemKind.Entity, 0, null, null,
                    itemEntity, null, null, null, null, null, null));
            }
            if (subWorkspace is not null)
            {
                items.Add(new WorkspaceItemDescriptor(
                    WorkspaceItemKind.SubWorkspace, 0, null, null,
                    null, null, null, null, null, subWorkspace, null));
            }

            _descriptor = new WorkspaceDescriptor
            {
                Name = name,
                Order = order,
                IsShell = shell,
                Sections = items.Count == 0
                    ? []
                    : [new WorkspaceSectionDescriptor(sectionKey ?? "default", null, 0, false, items)],
            };
        }

        public string Name { get; }
        public WorkspaceDescriptor Descriptor => _descriptor;
    }

    private sealed class FakeContributor(string targetName, string sectionKey, string entityName)
        : IWorkspaceContributor
    {
        public void Contribute(IWorkspaceContributionContext context)
        {
            context.ForWorkspace(targetName).Section(sectionKey, s => s.Entity(entityName));
        }
    }
}
