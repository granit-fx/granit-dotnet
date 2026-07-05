using Shouldly;
using Xunit;

namespace Granit.Workspaces.Abstractions.Tests;

public sealed class WorkspaceDefinitionTests
{
    [Fact]
    public void Descriptor_is_built_once_and_cached()
    {
        CountingDefinition def = new();

        WorkspaceDescriptor first = def.Descriptor;
        WorkspaceDescriptor second = def.Descriptor;

        ReferenceEquals(first, second).ShouldBeTrue();
        def.ConfigureCallCount.ShouldBe(1);
    }

    [Fact]
    public void Descriptor_exposes_the_name_set_by_the_subclass()
    {
        CountingDefinition def = new();
        def.Descriptor.Name.ShouldBe(def.Name);
    }

    [Fact]
    public void Builder_DisplayKey_blank_throws() =>
        Should.Throw<ArgumentException>(() => new BlankDisplayKey().Descriptor);

    [Fact]
    public void Builder_Icon_blank_throws() =>
        Should.Throw<ArgumentException>(() => new BlankIcon().Descriptor);

    [Fact]
    public void Builder_RequiresPermission_blank_throws() =>
        Should.Throw<ArgumentException>(() => new BlankPermission().Descriptor);

    [Fact]
    public void Defaults_are_null_or_zero_when_unset()
    {
        BareDefinition def = new();
        WorkspaceDescriptor d = def.Descriptor;

        d.DisplayKey.ShouldBeNull();
        d.Icon.ShouldBeNull();
        d.Order.ShouldBe(0);
        d.RequiresPermission.ShouldBeNull();
        d.IsShell.ShouldBeFalse();
        d.Sections.ShouldBeEmpty();
    }

    private sealed class CountingDefinition : WorkspaceDefinition
    {
        public int ConfigureCallCount { get; private set; }
        public override string Name => "Counted";

        protected override void Configure(WorkspaceBuilder builder)
        {
            ConfigureCallCount++;
            builder.Order(3);
        }
    }

    private sealed class BareDefinition : WorkspaceDefinition
    {
        public override string Name => "Bare";
        protected override void Configure(WorkspaceBuilder builder) { }
    }

    private sealed class BlankDisplayKey : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder builder) => builder.DisplayKey("  ");
    }

    private sealed class BlankIcon : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder builder) => builder.Icon(" ");
    }

    private sealed class BlankPermission : WorkspaceDefinition
    {
        public override string Name => "X";
        protected override void Configure(WorkspaceBuilder builder) => builder.RequiresPermission("");
    }
}
