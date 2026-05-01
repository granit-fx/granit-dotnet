using Granit.Entities.Relations;
using Granit.Invoicing.Endpoints.Relations;
using Granit.Parties.Domain;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Endpoints.Tests;

public sealed class InvoicesOnPartyRelationContributionTests
{
    [Fact]
    public void Contribute_grafts_invoices_relation_onto_Party()
    {
        EntityRelationContributionContext context = new();

        new InvoicesOnPartyRelationContribution().Contribute(context);

        IReadOnlyList<RelationDescriptor> relations = context.Contributions[typeof(Party)];
        relations.ShouldHaveSingleItem();

        RelationDescriptor invoices = relations.Single();
        invoices.Name.ShouldBe("invoices");
        invoices.TargetEntityName.ShouldBe("Granit.Invoicing.Invoice");
        invoices.Display.ShouldBe(RelationDisplay.SmartButton);
        invoices.RequiresPermission.ShouldBe("Invoicing.Invoices.Read");
        invoices.Aggregates.Select(a => a.Kind).ShouldBe(
            [RelationAggregateKind.Count, RelationAggregateKind.Sum]);
        invoices.ContributorAssemblyName.ShouldNotBeNull();
        invoices.ShowOnKanbanCard.ShouldBeTrue("the invoice counter is pinned on the Party kanban tile");
    }
}
