using Shouldly;
using Xunit;

namespace Granit.Workspaces.Abstractions.Tests;

public sealed class WorkspaceItemBuilderTests
{
    [Fact]
    public void Entity_item_carries_metadata_view_and_preset_overlay()
    {
        EntityItemDefinition def = new();
        WorkspaceItemDescriptor item = def.Descriptor.Sections[0].Items.Single();

        item.Kind.ShouldBe(WorkspaceItemKind.Entity);
        item.EntityName.ShouldBe("Mod.Customer");
        item.EntityViewName.ShouldBe("open");
        item.DisplayKey.ShouldBe("Customer:Label");
        item.Icon.ShouldBe("user");
        item.Order.ShouldBe(5);
        item.RequiresPermission.ShouldBe("Mod.Customers.Read");
        item.EntityPresetOverlay.ShouldNotBeNull();
        item.EntityPresetOverlay!["status"].ShouldBe("active");
    }

    [Fact]
    public void Typed_entity_uses_type_full_name_as_wire_id()
    {
        TypedEntityDefinition def = new();
        WorkspaceItemDescriptor item = def.Descriptor.Sections[0].Items.Single();

        item.EntityName.ShouldBe(typeof(SampleEntityMarker).FullName);
    }

    [Fact]
    public void Dashboard_item_keeps_dashboard_name()
    {
        DashboardItemDefinition def = new();
        WorkspaceItemDescriptor item = def.Descriptor.Sections[0].Items.Single();

        item.Kind.ShouldBe(WorkspaceItemKind.Dashboard);
        item.DashboardName.ShouldBe("Mod.SalesDashboard");
        item.EntityName.ShouldBeNull();
        item.LinkUrl.ShouldBeNull();
    }

    [Fact]
    public void Link_item_keeps_link_url()
    {
        LinkItemDefinition def = new();
        WorkspaceItemDescriptor item = def.Descriptor.Sections[0].Items.Single();

        item.Kind.ShouldBe(WorkspaceItemKind.Link);
        item.LinkUrl.ShouldBe("https://example.test");
    }

    [Fact]
    public void SubWorkspace_item_keeps_sub_workspace_name()
    {
        SubWorkspaceItemDefinition def = new();
        WorkspaceItemDescriptor item = def.Descriptor.Sections[0].Items.Single();

        item.Kind.ShouldBe(WorkspaceItemKind.SubWorkspace);
        item.SubWorkspaceName.ShouldBe("Mod.Child");
    }

    [Fact]
    public void Preset_cannot_be_called_on_link() =>
        Should.Throw<InvalidOperationException>(() => new InvalidPresetOnLink().Descriptor);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void DisplayKey_rejects_blank(string value) =>
        Should.Throw<ArgumentException>(() => new BlankDisplayKey(value).Descriptor);

    [Fact]
    public void Items_are_ordered_by_order_value()
    {
        OrderingDefinition def = new();
        IReadOnlyList<WorkspaceItemDescriptor> items = def.Descriptor.Sections[0].Items;

        items.Select(i => i.Order).ShouldBe([1, 5, 10]);
    }

    [Fact]
    public void Preset_throws_when_dictionary_is_null() =>
        Should.Throw<ArgumentNullException>(() => new NullPresetDefinition().Descriptor);

    private sealed class SampleEntityMarker;

    private sealed class EntityItemDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Entity("Mod.Customer", i => i
                .View("open")
                .DisplayKey("Customer:Label")
                .Icon("user")
                .Order(5)
                .RequiresPermission("Mod.Customers.Read")
                .Preset(new Dictionary<string, object?> { ["status"] = "active" })));
    }

    private sealed class TypedEntityDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Entity<SampleEntityMarker>());
    }

    private sealed class DashboardItemDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Dashboard("Mod.SalesDashboard"));
    }

    private sealed class LinkItemDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Link("https://example.test"));
    }

    private sealed class SubWorkspaceItemDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.SubWorkspace("Mod.Child"));
    }

    private sealed class InvalidPresetOnLink : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Link("/x", i =>
                i.Preset(new Dictionary<string, object?> { ["k"] = 1 })));
    }

    private sealed class BlankDisplayKey(string key) : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Entity("E", i => i.DisplayKey(key)));
    }

    private sealed class OrderingDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s
                .Entity("A", i => i.Order(10))
                .Entity("B", i => i.Order(1))
                .Entity("C", i => i.Order(5)));
    }

    private sealed class NullPresetDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Entity("E", i => i.Preset(null!)));
    }
}
