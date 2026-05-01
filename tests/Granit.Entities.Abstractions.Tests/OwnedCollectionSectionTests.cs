using Granit.Entities.Forms;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests;

public sealed class OwnedCollectionSectionTests
{
    [Fact]
    public void OwnedCollectionSection_carries_property_name_item_type_and_fields()
    {
        EntityDefinitionDescriptor d = new SampleOwnerDefinition().Descriptor;

        FormDescriptor form = d.Forms.Single(f => f.Name == "default");
        SectionDescriptor section = form.Sections.Single(s => s.Key == "addresses");

        section.OwnedCollection.ShouldNotBeNull();
        section.Fields.ShouldBeEmpty();

        OwnedCollectionDescriptor owned = section.OwnedCollection!;
        owned.PropertyName.ShouldBe("Addresses");
        owned.ItemType.ShouldBe(typeof(SampleAddress));
        owned.ItemFields.Select(f => f.PropertyName).ShouldBe(["Line1", "City", "Country"]);
        owned.ItemDisplayProperty.ShouldBe("Line1");
    }

    [Fact]
    public void OwnedCollectionSection_preserves_per_item_field_options()
    {
        EntityDefinitionDescriptor d = new SampleOwnerDefinition().Descriptor;

        FormDescriptor form = d.Forms.Single(f => f.Name == "default");
        SectionDescriptor section = form.Sections.Single(s => s.Key == "addresses");

        FieldDescriptor country = section.OwnedCollection!.ItemFields.Single(f => f.PropertyName == "Country");
        country.ReadOnly.ShouldBeTrue();
        country.LabelKey.ShouldBe("Sample:Address.Country");
    }

    [Fact]
    public void OwnedCollectionSection_supports_label_collapsedByDefault_and_maxRendered()
    {
        EntityDefinitionDescriptor d = new SampleOwnerDefinition().Descriptor;

        FormDescriptor form = d.Forms.Single(f => f.Name == "default");
        SectionDescriptor section = form.Sections.Single(s => s.Key == "addresses");

        section.LabelKey.ShouldBe("Sample:Section.Addresses");
        section.CollapsedByDefault.ShouldBeTrue();
        section.OwnedCollection!.MaxRendered.ShouldBe(5);
    }

    [Fact]
    public void OwnedCollectionSection_orders_relative_to_scalar_sections_by_declaration()
    {
        EntityDefinitionDescriptor d = new SampleOwnerDefinition().Descriptor;

        FormDescriptor form = d.Forms.Single(f => f.Name == "default");

        form.Sections.Select(s => s.Key).ShouldBe(["identity", "addresses", "footer"]);
        form.Sections.Select(s => s.Order).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void OwnedCollectionSection_rejects_non_property_collection_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadCollectionLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("Collection selector must be a direct property access");
    }

    [Fact]
    public void OwnedCollectionSection_rejects_non_property_item_display_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadItemDisplayLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("ItemDisplayProperty selector must be a direct property access");
    }

    [Fact]
    public void OwnedCollectionSection_rejects_non_positive_MaxRendered()
    {
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            _ = new BadMaxRenderedDefinition().Descriptor);

        ex.ParamName.ShouldBe("max");
    }

    private sealed class SampleOwner
    {
        public string Name { get; set; } = string.Empty;
        public IReadOnlyList<SampleAddress> Addresses { get; set; } = [];
        public string? FooterNote { get; set; }
    }

    private sealed class SampleAddress
    {
        public string Line1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    private sealed class SampleOwnerDefinition : EntityDefinition<SampleOwner>
    {
        public override string Name => "Granit.Sample.Owner";

        protected override void Configure(EntityDefinitionBuilder<SampleOwner> builder) =>
            builder.Form("default", f => f
                .Section("identity", s => s.Field(o => o.Name))
                .OwnedCollectionSection<SampleAddress>("addresses", o => o.Addresses, s => s
                    .Label("Sample:Section.Addresses")
                    .CollapsedByDefault()
                    .MaxRendered(5)
                    .ItemField(a => a.Line1)
                    .ItemField(a => a.City)
                    .ItemField(a => a.Country, f => f
                        .ReadOnly()
                        .Label("Sample:Address.Country"))
                    .ItemDisplayProperty(a => a.Line1))
                .Section("footer", s => s.Field(o => o.FooterNote)));
    }

    private sealed class BadCollectionLambdaDefinition : EntityDefinition<SampleOwner>
    {
        public override string Name => "Granit.Sample.BadOwner";

        protected override void Configure(EntityDefinitionBuilder<SampleOwner> builder) =>
            builder.Form("default", f => f
                .OwnedCollectionSection<SampleAddress>("addresses", o => o.Addresses.Where(_ => true), s => s
                    .ItemField(a => a.Line1)));
    }

    private sealed class BadItemDisplayLambdaDefinition : EntityDefinition<SampleOwner>
    {
        public override string Name => "Granit.Sample.BadOwnerB";

        protected override void Configure(EntityDefinitionBuilder<SampleOwner> builder) =>
            builder.Form("default", f => f
                .OwnedCollectionSection<SampleAddress>("addresses", o => o.Addresses, s => s
                    .ItemField(a => a.Line1)
                    .ItemDisplayProperty(a => a.Line1.ToUpperInvariant())));
    }

    private sealed class BadMaxRenderedDefinition : EntityDefinition<SampleOwner>
    {
        public override string Name => "Granit.Sample.BadOwnerC";

        protected override void Configure(EntityDefinitionBuilder<SampleOwner> builder) =>
            builder.Form("default", f => f
                .OwnedCollectionSection<SampleAddress>("addresses", o => o.Addresses, s => s
                    .MaxRendered(0)
                    .ItemField(a => a.Line1)));
    }
}
