using Shouldly;
using Xunit;

namespace Granit.Workspaces.Abstractions.Tests;

public sealed class WorkspaceSectionBuilderTests
{
    [Fact]
    public void Section_stores_display_key_order_and_collapsed_flag()
    {
        ConfiguredSectionDefinition def = new();
        WorkspaceSectionDescriptor section = def.Descriptor.Sections.Single();

        section.Key.ShouldBe("reports");
        section.DisplayKey.ShouldBe("Section:Reports");
        section.Order.ShouldBe(7);
        section.CollapsedByDefault.ShouldBeTrue();
    }

    [Fact]
    public void CollapsedByDefault_default_overload_collapses()
    {
        DefaultCollapsedDefinition def = new();
        def.Descriptor.Sections.Single().CollapsedByDefault.ShouldBeTrue();
    }

    [Fact]
    public void Empty_section_has_empty_items()
    {
        EmptySectionDefinition def = new();
        def.Descriptor.Sections.Single().Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Section_key_rejects_blank(string key) =>
        Should.Throw<ArgumentException>(() => new BlankSectionKey(key).Descriptor);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Entity_name_rejects_blank(string name) =>
        Should.Throw<ArgumentException>(() => new BlankEntityName(name).Descriptor);

    [Fact]
    public void Section_throws_when_configure_is_null() =>
        Should.Throw<ArgumentNullException>(() => new NullConfigureDefinition().Descriptor);

    private sealed class ConfiguredSectionDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("reports", s => s
                .DisplayKey("Section:Reports")
                .Order(7)
                .CollapsedByDefault(true));
    }

    private sealed class DefaultCollapsedDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.CollapsedByDefault());
    }

    private sealed class EmptySectionDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) => b.Section("s", _ => { });
    }

    private sealed class BlankSectionKey(string key) : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) => b.Section(key, _ => { });
    }

    private sealed class BlankEntityName(string n) : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) =>
            b.Section("s", s => s.Entity(n));
    }

    private sealed class NullConfigureDefinition : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder b) => b.Section("s", null!);
    }
}
