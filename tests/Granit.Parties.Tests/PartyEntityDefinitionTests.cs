using Granit.Entities;
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
