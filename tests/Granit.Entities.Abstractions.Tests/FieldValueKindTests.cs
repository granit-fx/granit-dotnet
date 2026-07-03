using Granit.Entities.Forms;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests;

public sealed class FieldValueKindTests
{
    [Fact]
    public void Field_without_value_kind_has_null_value_kind()
    {
        FieldDescriptor title = FieldsOf(new SampleEntityDefinition())["Title"];

        title.ValueKind.ShouldBeNull();
    }

    [Fact]
    public void ValueKind_is_captured_on_the_descriptor()
    {
        FieldDescriptor homepage = FieldsOf(new SampleEntityDefinition())["Homepage"];

        homepage.ValueKind.ShouldBe(ValueKind.Url);
    }

    [Fact]
    public void ValueKind_does_not_override_an_explicit_component()
    {
        FieldDescriptor homepage = FieldsOf(new SampleEntityDefinition())["Homepage"];

        // ValueKind is a semantic hint alongside Component, not a replacement — the explicit
        // component the caller set stays intact.
        homepage.Component.ShouldBe("url");
    }

    private static Dictionary<string, FieldDescriptor> FieldsOf(SampleEntityDefinition definition) =>
        definition.Descriptor.Forms[0].Sections
            .SelectMany(s => s.Fields)
            .ToDictionary(f => f.PropertyName);

    private sealed class SampleEntity
    {
        public string Title { get; set; } = "";
        public string Homepage { get; set; } = "";
    }

    private sealed class SampleEntityDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.ValueKindEntity";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> b) =>
            b.Form("default", f => f
                .Section("general", s => s
                    .Field(x => x.Title)
                    .Field(x => x.Homepage, fld => fld.Component("url").ValueKind(ValueKind.Url))));
    }
}
