using Granit.Entities;
using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Customization.Endpoints.Internal;
using Granit.Entities.Forms;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Entities.Customization.Endpoints.Tests.Internal;

public sealed class DescriptorDeltaValidatorTests
{
    private const string EntityName = "Granit.Parties.Party";

    [Fact]
    public void Unknown_entity_fails_with_message()
    {
        IEntityDefinitionRegistry registry = Substitute.For<IEntityDefinitionRegistry>();
        registry.GetByName(EntityName).Returns((IEntityDefinitionDescriptor?)null);
        DescriptorDeltaValidator sut = new(registry);

        ValidationResult result = sut.Validate(EntityName, LayoutKind.FormDefault, [new HideDelta("a")]);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("Unknown entity");
    }

    [Fact]
    public void Form_default_with_known_field_succeeds()
    {
        DescriptorDeltaValidator sut = SutFor(BuildFormDescriptor(("general", ["FirstName", "LastName"])));

        ValidationResult result = sut.Validate(EntityName, LayoutKind.FormDefault,
            [new HideDelta("FirstName"), new RegroupDelta("LastName", "general")]);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Form_default_with_dangling_field_fails()
    {
        DescriptorDeltaValidator sut = SutFor(BuildFormDescriptor(("general", ["FirstName"])));

        ValidationResult result = sut.Validate(EntityName, LayoutKind.FormDefault,
            [new HideDelta("Phantom")]);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("Phantom");
    }

    [Fact]
    public void Form_default_with_unknown_regroup_target_fails()
    {
        DescriptorDeltaValidator sut = SutFor(BuildFormDescriptor(("general", ["FirstName"])));

        ValidationResult result = sut.Validate(EntityName, LayoutKind.FormDefault,
            [new RegroupDelta("FirstName", "phantom-group")]);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("phantom-group");
    }

    [Fact]
    public void Form_default_with_dangling_reorder_anchor_fails()
    {
        DescriptorDeltaValidator sut = SutFor(BuildFormDescriptor(("general", ["FirstName", "LastName"])));

        ValidationResult result = sut.Validate(EntityName, LayoutKind.FormDefault,
            [new ReorderDelta("FirstName", BeforeFieldName: "Phantom", AfterFieldName: null)]);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("Phantom");
    }

    [Fact]
    public void List_layout_kind_skips_semantic_validation()
    {
        // No List descriptor on EntityDefinitionDescriptor — validator delegates to the
        // manifest composer (B4), so any payload is accepted at this layer.
        DescriptorDeltaValidator sut = SutFor(BuildFormDescriptor(("general", ["FirstName"])));

        ValidationResult result = sut.Validate(EntityName, LayoutKind.List,
            [new HideDelta("AnythingGoes")]);

        result.IsValid.ShouldBeTrue();
    }

    private static DescriptorDeltaValidator SutFor(EntityDefinitionDescriptor descriptor)
    {
        IEntityDefinitionDescriptor wrapper = Substitute.For<IEntityDefinitionDescriptor>();
        wrapper.Name.Returns(EntityName);
        wrapper.EntityType.Returns(typeof(object));
        wrapper.Descriptor.Returns(descriptor);

        IEntityDefinitionRegistry registry = Substitute.For<IEntityDefinitionRegistry>();
        registry.GetByName(EntityName).Returns(wrapper);

        return new DescriptorDeltaValidator(registry);
    }

    private static EntityDefinitionDescriptor BuildFormDescriptor(params (string SectionKey, string[] Fields)[] sections)
    {
        FormDescriptor form = new()
        {
            Name = "default",
            Sections = [.. sections.Select(s => new SectionDescriptor
            {
                Key = s.SectionKey,
                Fields = [.. s.Fields.Select(f => new FieldDescriptor
                {
                    PropertyName = f,
                    ClrType = typeof(string),
                    Component = "text",
                })],
            })],
        };

        return new EntityDefinitionDescriptor
        {
            Name = EntityName,
            EntityType = typeof(object),
            Forms = [form],
            Details = [],
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
        };
    }
}
