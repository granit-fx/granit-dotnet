using Granit.Entities;
using Granit.Entities.Forms;
using Granit.Entities.Layouts;
using Granit.Invoicing.Domain;
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
    public void Descriptor_default_form_carries_scalar_and_owned_collection_sections_in_declaration_order()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        FormDescriptor defaultForm = d.Forms.Single(f => f.Name == "default");

        defaultForm.Sections.Select(s => s.Key).ShouldBe(
            ["identity", "billing", "amounts", "lineItems", "externalReferences", "documents"]);
    }

    [Fact]
    public void Descriptor_lineItems_owned_collection_targets_InvoiceLineItem_with_description_as_display_property()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        SectionDescriptor lineItems = d.Forms.Single(f => f.Name == "default")
            .Sections.Single(s => s.Key == "lineItems");

        lineItems.OwnedCollection.ShouldNotBeNull();
        lineItems.OwnedCollection!.PropertyName.ShouldBe("LineItems");
        lineItems.OwnedCollection.ItemType.ShouldBe(typeof(InvoiceLineItem));
        lineItems.OwnedCollection.ItemDisplayProperty.ShouldBe("Description");
        lineItems.OwnedCollection.ItemFields.Select(f => f.PropertyName)
            .ShouldBe(["Description", "Quantity", "UnitPrice", "Amount", "TaxRate", "TaxAmount"]);
    }

    [Fact]
    public void Descriptor_lineItems_marks_computed_amounts_as_read_only()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        OwnedCollectionDescriptor lineItems = d.Forms.Single(f => f.Name == "default")
            .Sections.Single(s => s.Key == "lineItems")
            .OwnedCollection!;

        lineItems.ItemFields.Single(f => f.PropertyName == "Amount").ReadOnly.ShouldBeTrue();
        lineItems.ItemFields.Single(f => f.PropertyName == "TaxAmount").ReadOnly.ShouldBeTrue();
        lineItems.ItemFields.Single(f => f.PropertyName == "Quantity").ReadOnly.ShouldBeFalse();
    }

    [Fact]
    public void Descriptor_documents_section_targets_InvoiceDocument_and_is_collapsed()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        SectionDescriptor documents = d.Forms.Single(f => f.Name == "default")
            .Sections.Single(s => s.Key == "documents");

        documents.CollapsedByDefault.ShouldBeTrue();
        documents.OwnedCollection!.ItemType.ShouldBe(typeof(InvoiceDocument));
        documents.OwnedCollection.ItemFields.ShouldAllBe(f => f.ReadOnly);
    }

    [Fact]
    public void Descriptor_default_form_groups_amounts_separately_from_identity()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        FormDescriptor defaultForm = d.Forms.Single(f => f.Name == "default");
        defaultForm.Sections.Select(s => s.Key).ShouldContain("identity");
        defaultForm.Sections.Select(s => s.Key).ShouldContain("amounts");
    }

    [Fact]
    public void Descriptor_references_query_export_and_metric_definitions()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        d.QueryDefinitionType.ShouldNotBeNull();
        d.ExportDefinitionType.ShouldNotBeNull();
        d.MetricDefinitionTypes.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Descriptor_exposes_kanban_layout_grouped_by_status()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        KanbanLayoutDescriptor kanban = d.ListLayouts.OfType<KanbanLayoutDescriptor>().ShouldHaveSingleItem();
        kanban.GroupByPropertyName.ShouldBe("Status");
        kanban.GroupByClrType.ShouldBe(typeof(InvoiceStatus));
    }

    [Fact]
    public void Descriptor_kanban_card_uses_invoice_number_as_title_and_lists_party_due_total()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;
        var kanban = (KanbanLayoutDescriptor)d.ListLayouts.Single(l => l.Kind == EntityListLayoutKind.Kanban);

        kanban.Card.TitleProperty.ShouldBe("InvoiceNumber");
        kanban.Card.Fields.Select(f => f.PropertyName).ShouldBe(["PartyId", "DueAt", "Total"]);
    }

    [Fact]
    public void Descriptor_kanban_columns_carry_status_specific_color_and_state()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;
        var kanban = (KanbanLayoutDescriptor)d.ListLayouts.Single(l => l.Kind == EntityListLayoutKind.Kanban);

        kanban.Columns.Single(c => c.Value == nameof(InvoiceStatus.Draft)).Color.ShouldBe(KanbanColor.Gray);
        kanban.Columns.Single(c => c.Value == nameof(InvoiceStatus.Open)).Color.ShouldBe(KanbanColor.Orange);
        kanban.Columns.Single(c => c.Value == nameof(InvoiceStatus.Paid)).Color.ShouldBe(KanbanColor.Green);
        kanban.Columns.Single(c => c.Value == nameof(InvoiceStatus.Void)).DefaultState.ShouldBe(KanbanColumnState.Hidden);
        kanban.Columns.Single(c => c.Value == nameof(InvoiceStatus.Uncollectible)).DefaultState.ShouldBe(KanbanColumnState.Collapsed);
    }

    [Fact]
    public void Descriptor_exposes_lifecycle_actions_in_declaration_order()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;

        d.Actions.Select(a => a.Name).ShouldBe(
            ["finalize", "void", "mark-uncollectible", "download-pdf"]);
    }

    [Fact]
    public void Descriptor_finalize_action_is_apicall_post_with_confirmation_and_manage_perm()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;
        Granit.Entities.Actions.EntityActionDescriptor finalize = d.Actions.Single(a => a.Name == "finalize");

        finalize.Kind.ShouldBe(Granit.Entities.Actions.EntityActionKind.ApiCall);
        finalize.HttpMethod.ShouldBe("POST");
        finalize.UrlTemplate.ShouldBe("/api/v1/invoices/{id}/finalize");
        finalize.RequiresPermission.ShouldBe("Invoicing.Invoices.Manage");
        finalize.ConfirmationKey.ShouldBe("Invoicing:Action.Finalize.Confirm");
    }

    [Fact]
    public void Descriptor_download_pdf_action_is_download_kind_no_method_no_confirmation()
    {
        EntityDefinitionDescriptor d = new InvoiceEntityDefinition().Descriptor;
        Granit.Entities.Actions.EntityActionDescriptor pdf = d.Actions.Single(a => a.Name == "download-pdf");

        pdf.Kind.ShouldBe(Granit.Entities.Actions.EntityActionKind.Download);
        pdf.HttpMethod.ShouldBeNull();
        pdf.UrlTemplate.ShouldBe("/api/v1/invoices/{id}/pdf");
        pdf.ConfirmationKey.ShouldBeNull();
        pdf.RequiresPermission.ShouldBe("Invoicing.Invoices.Read");
    }
}
