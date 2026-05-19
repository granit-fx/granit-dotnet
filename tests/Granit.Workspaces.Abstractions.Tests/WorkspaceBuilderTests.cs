using Shouldly;
using Xunit;

namespace Granit.Workspaces.Abstractions.Tests;

public sealed class WorkspaceBuilderTests
{
    [Fact]
    public void Build_assembles_a_descriptor_with_metadata_and_sections()
    {
        SampleDefinition def = new();
        WorkspaceDescriptor descriptor = def.Descriptor;

        descriptor.Name.ShouldBe("Sample");
        descriptor.DisplayKey.ShouldBe("Workspace:Sample");
        descriptor.Icon.ShouldBe("layout-grid");
        descriptor.Order.ShouldBe(10);
        descriptor.RequiresPermission.ShouldBe("Workspace.Sample.Read");
        descriptor.IsShell.ShouldBeFalse();

        descriptor.Sections.Count.ShouldBe(2);
        descriptor.Sections[0].Key.ShouldBe("identity");
        descriptor.Sections[0].Items.Single().Kind.ShouldBe(WorkspaceItemKind.Entity);
        descriptor.Sections[0].Items.Single().EntityName.ShouldBe("Sample.Customer");

        descriptor.Sections[1].Key.ShouldBe("reports");
        descriptor.Sections[1].Items.Select(i => i.Kind).ShouldBe(
            [WorkspaceItemKind.Dashboard, WorkspaceItemKind.Link, WorkspaceItemKind.SubWorkspace]);
    }

    [Fact]
    public void Build_throws_on_duplicate_section_keys()
    {
        DuplicateSectionDefinition def = new();
        Should.Throw<InvalidOperationException>(() => _ = def.Descriptor)
            .Message.ShouldContain("Duplicate workspace section key");
    }

    [Fact]
    public void Shell_builder_marks_workspace_as_shell()
    {
        ShellDefinition def = new();
        def.Descriptor.IsShell.ShouldBeTrue();
    }

    [Fact]
    public void View_can_only_be_called_on_entity_items() =>
        Should.Throw<InvalidOperationException>(() => new InvalidViewOnLink().Descriptor);

    [Fact]
    public void Feature_item_carries_feature_name_and_optional_route_override()
    {
        FeatureDefinition def = new();
        WorkspaceItemDescriptor item = def.Descriptor.Sections.Single().Items.Single();

        item.Kind.ShouldBe(WorkspaceItemKind.Feature);
        item.FeatureName.ShouldBe("invoicing.invoices.list");
        item.RouteName.ShouldBe("invoicing.invoices.list.tenant-view");
        item.DisplayKey.ShouldBe("Showcase:Workspace.Erp.InvoiceList");
        item.Icon.ShouldBe("chart-bar");
    }

    [Fact]
    public void RouteName_can_only_be_called_on_feature_items() =>
        Should.Throw<InvalidOperationException>(() => new InvalidRouteNameOnLink().Descriptor);

    private sealed class SampleDefinition : WorkspaceDefinition
    {
        public override string Name => "Sample";

        protected override void Configure(WorkspaceBuilder b) =>
            b.DisplayKey("Workspace:Sample")
                .Icon("layout-grid")
                .Order(10)
                .RequiresPermission("Workspace.Sample.Read")
                .Section("identity", s => s
                    .Entity("Sample.Customer", i => i.View("open").Order(0)))
                .Section("reports", s => s
                    .Dashboard("Sample.SalesDashboard", i => i.Order(0))
                    .Link("/external", i => i.Order(1))
                    .SubWorkspace("Sample.Crm", i => i.Order(2)));
    }

    private sealed class DuplicateSectionDefinition : WorkspaceDefinition
    {
        public override string Name => "Dup";

        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("a", _ => { }).Section("a", _ => { });
    }

    private sealed class ShellDefinition : WorkspaceDefinition
    {
        public override string Name => "Granit.Framework.Data";
        protected override void Configure(WorkspaceBuilder b) => b.Shell();
    }

    private sealed class InvalidViewOnLink : WorkspaceDefinition
    {
        public override string Name => "Invalid";

        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Link("/x", i => i.View("any")));
    }

    private sealed class FeatureDefinition : WorkspaceDefinition
    {
        public override string Name => "Showcase.Erp";

        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("billing", s => s
                .Feature("invoicing.invoices.list", i => i
                    .DisplayKey("Showcase:Workspace.Erp.InvoiceList")
                    .Icon("chart-bar")
                    .RouteName("invoicing.invoices.list.tenant-view")));
    }

    private sealed class InvalidRouteNameOnLink : WorkspaceDefinition
    {
        public override string Name => "Invalid";

        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Link("/x", i => i.RouteName("any.route")));
    }
}

public sealed class FeatureBuilderTests
{
    [Fact]
    public void Build_assembles_a_descriptor()
    {
        FeatureBuilder builder = new("invoicing.invoices.list");
        builder
            .Permission("Invoicing.Invoices.Read")
            .RouteName("invoicing.invoices.list")
            .DefaultIcon("receipt")
            .DisplayKey("InvoicingEndpoints:Invoices.List");

        FeatureDescriptor descriptor = builder.Build();

        descriptor.Name.ShouldBe("invoicing.invoices.list");
        descriptor.Permission.ShouldBe("Invoicing.Invoices.Read");
        descriptor.RouteName.ShouldBe("invoicing.invoices.list");
        descriptor.DefaultIcon.ShouldBe("receipt");
        descriptor.DisplayKey.ShouldBe("InvoicingEndpoints:Invoices.List");
    }

    [Fact]
    public void RouteName_defaults_to_feature_name_when_omitted()
    {
        FeatureBuilder builder = new("identity.users.list");
        builder
            .Permission("Identity.Users.Read")
            .DisplayKey("IdentityEndpoints:Users.List");

        FeatureDescriptor descriptor = builder.Build();
        descriptor.RouteName.ShouldBe("identity.users.list");
    }

    [Fact]
    public void Build_throws_when_permission_missing()
    {
        FeatureBuilder builder = new("x.y.z");
        builder.DisplayKey("X:Y.Z");
        Should.Throw<InvalidOperationException>(() => builder.Build())
            .Message.ShouldContain("permission");
    }

    [Fact]
    public void Build_throws_when_display_key_missing()
    {
        FeatureBuilder builder = new("x.y.z");
        builder.Permission("X.Y.Read");
        Should.Throw<InvalidOperationException>(() => builder.Build())
            .Message.ShouldContain("display key");
    }
}
