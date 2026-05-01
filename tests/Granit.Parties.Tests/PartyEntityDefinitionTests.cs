using Granit.Entities;
using Granit.Entities.Forms;
using Granit.Parties.Domain;
using Granit.Parties.Entities;
using Shouldly;
using Xunit;

namespace Granit.Parties.Tests;

public sealed class PartyEntityDefinitionTests
{
    [Fact]
    public void Descriptor_carries_identity_metadata()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        d.Name.ShouldBe("Granit.Parties.Party");
        d.DisplayKey.ShouldBe("Parties:Entity.Party");
        d.Icon.ShouldBe("users");
        d.PermissionGroup.ShouldBe("Parties.Parties");
        d.DisplayProperty.ShouldBe("Name");
    }

    [Fact]
    public void Descriptor_declares_default_and_quick_form_variants()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        d.Forms.Select(f => f.Name).ShouldBe(["default", "quick"]);
    }

    [Fact]
    public void Descriptor_default_form_carries_scalar_and_owned_collection_sections_in_declaration_order()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        FormDescriptor defaultForm = d.Forms.Single(f => f.Name == "default");

        defaultForm.Sections.Select(s => s.Key).ShouldBe(
            ["identity", "locale", "contact", "emails", "phones", "addresses", "externalMappings", "tax"]);
    }

    [Fact]
    public void Descriptor_emails_owned_collection_targets_PartyEmail_with_address_as_display_property()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        SectionDescriptor emails = d.Forms.Single(f => f.Name == "default")
            .Sections.Single(s => s.Key == "emails");

        emails.OwnedCollection.ShouldNotBeNull();
        emails.OwnedCollection!.PropertyName.ShouldBe("Emails");
        emails.OwnedCollection.ItemType.ShouldBe(typeof(PartyEmail));
        emails.OwnedCollection.ItemDisplayProperty.ShouldBe("Address");
        emails.OwnedCollection.ItemFields.Select(f => f.PropertyName)
            .ShouldBe(["Address", "Label", "IsPrimary"]);
    }

    [Fact]
    public void Descriptor_addresses_owned_collection_targets_PartyAddress()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        SectionDescriptor addresses = d.Forms.Single(f => f.Name == "default")
            .Sections.Single(s => s.Key == "addresses");

        addresses.OwnedCollection!.PropertyName.ShouldBe("Addresses");
        addresses.OwnedCollection.ItemType.ShouldBe(typeof(PartyAddress));
    }

    [Fact]
    public void Descriptor_external_mappings_section_is_collapsed_by_default()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        SectionDescriptor mappings = d.Forms.Single(f => f.Name == "default")
            .Sections.Single(s => s.Key == "externalMappings");

        mappings.CollapsedByDefault.ShouldBeTrue();
        mappings.OwnedCollection!.PropertyName.ShouldBe("ExternalMappings");
    }

    [Fact]
    public void Descriptor_tax_section_exposes_TaxStatus_as_read_only()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        SectionDescriptor tax = d.Forms.Single(f => f.Name == "default")
            .Sections.Single(s => s.Key == "tax");

        FieldDescriptor taxStatus = tax.Fields.Single(f => f.PropertyName == "TaxStatus");
        taxStatus.ReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void Descriptor_declares_a_default_detail_with_audit_and_timeline_panels()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        d.Details.ShouldHaveSingleItem();
        d.Details.Single().Name.ShouldBe("default");
        d.Details.Single().SidePanels.Select(p => p.Kind).ShouldContain(Granit.Entities.Details.SidePanelKind.Audit);
        d.Details.Single().SidePanels.Select(p => p.Kind).ShouldContain(Granit.Entities.Details.SidePanelKind.Timeline);
    }

    [Fact]
    public void Descriptor_references_query_export_and_metric_definitions()
    {
        EntityDefinitionDescriptor d = new PartyEntityDefinition().Descriptor;

        d.QueryDefinitionType.ShouldNotBeNull();
        d.ExportDefinitionType.ShouldNotBeNull();
        d.MetricDefinitionTypes.ShouldNotBeEmpty();
    }
}
