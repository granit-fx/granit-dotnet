using Granit.Entities;
using Granit.Invoicing.Entities;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Tests;

public sealed class InvoiceEntityDefinitionTests
{
    [Fact]
    public void Descriptor_carries_identity_metadata()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        d.Name.ShouldBe("Granit.Invoicing.Invoice");
        d.DisplayKey.ShouldBe("Invoicing:Entity.Invoice");
        d.Icon.ShouldBe("file-text");
        d.PermissionGroup.ShouldBe("Invoicing.Invoices");
        d.DisplayProperty.ShouldBe("InvoiceNumber");
    }

    [Fact]
    public void Descriptor_declares_default_and_quick_form_variants()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        d.Forms.Select(f => f.Name).ShouldBe(["default", "quick"]);
    }

    [Fact]
    public void Descriptor_default_form_groups_amounts_separately_from_identity()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        Granit.Entities.Forms.FormDescriptor defaultForm = d.Forms.Single(f => f.Name == "default");
        defaultForm.Sections.Select(s => s.Key).ShouldBe(["identity", "billing", "amounts"]);
    }

    [Fact]
    public void Descriptor_references_query_export_and_metric_definitions()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        d.QueryDefinitionType.ShouldNotBeNull();
        d.ExportDefinitionType.ShouldNotBeNull();
        d.MetricDefinitionTypes.Count.ShouldBeGreaterThan(0);
    }
}
