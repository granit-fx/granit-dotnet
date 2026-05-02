using Granit.Entities.Activities;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests.Activities;

public sealed class ActivitiesOptionsBuilderTests
{
    private sealed class SampleEntity
    {
        public Guid AccountManagerUserId { get; set; }
        public string Name { get; set; } = "";
    }

    [Fact]
    public void EntityDefinitionBuilder_Activities_with_no_configure_produces_empty_descriptor()
    {
        EntityDefinition<SampleEntity> def = new SampleEntityDefinition(b => b.Activities());
        EntityDefinitionDescriptor descriptor = def.Descriptor;
        descriptor.Activities.ShouldNotBeNull();
        descriptor.Activities!.AllowedTypeNames.ShouldBeEmpty();
        descriptor.Activities.DefaultAssigneePropertyName.ShouldBeNull();
    }

    [Fact]
    public void EntityDefinitionBuilder_Activities_AllowedTypes_captures_names_in_order()
    {
        EntityDefinition<SampleEntity> def = new SampleEntityDefinition(b =>
            b.Activities(a => a.AllowedTypes("Call", "Meeting", "Email")));
        EntityDefinitionDescriptor descriptor = def.Descriptor;
        descriptor.Activities!.AllowedTypeNames.ShouldBe(["Call", "Meeting", "Email"]);
    }

    [Fact]
    public void EntityDefinitionBuilder_Activities_DefaultAssignee_captures_property_name()
    {
        EntityDefinition<SampleEntity> def = new SampleEntityDefinition(b =>
            b.Activities(a => a.DefaultAssignee(e => e.AccountManagerUserId)));
        EntityDefinitionDescriptor descriptor = def.Descriptor;
        descriptor.Activities!.DefaultAssigneePropertyName.ShouldBe("AccountManagerUserId");
    }

    [Fact]
    public void Activities_descriptor_absent_when_builder_method_not_called()
    {
        EntityDefinition<SampleEntity> def = new SampleEntityDefinition(_ => { });
        EntityDefinitionDescriptor descriptor = def.Descriptor;
        descriptor.Activities.ShouldBeNull();
    }

    [Fact]
    public void AllowedTypes_with_no_arguments_throws()
    {
        ActivitiesOptionsBuilder<SampleEntity> sut = new();
        Should.Throw<ArgumentException>(() => sut.AllowedTypes());
    }

    [Fact]
    public void DefaultAssignee_with_non_property_lambda_throws()
    {
        ActivitiesOptionsBuilder<SampleEntity> sut = new();
        Should.Throw<ArgumentException>(() => sut.DefaultAssignee(e => e.Name + "!"));
    }

    private sealed class SampleEntityDefinition(Action<EntityDefinitionBuilder<SampleEntity>> apply)
        : EntityDefinition<SampleEntity>
    {
        public override string Name => "Test.SampleEntity";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> b)
        {
            b.PermissionGroup("Test.Samples");
            apply(b);
        }
    }
}
