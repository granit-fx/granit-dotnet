using Granit.Entities.Relations;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Endpoints.Permissions;
using Granit.Parties.Domain;

namespace Granit.Invoicing.Endpoints.Relations;

/// <summary>
/// Phase 1.F-β cobaye — grafts an <c>"invoices"</c> smart-button relation
/// onto <c>Granit.Parties.Party</c>, surfacing <c>Count</c> + <c>Sum(Total)</c>
/// in one round-trip on the party detail header.
/// </summary>
internal sealed class InvoicesOnPartyRelationContribution : IEntityRelationContributor
{
    public void Contribute(IEntityRelationContributionContext context) =>
        context.AddRelation<Party, Invoice>(
            name: "invoices",
            targetEntityName: "Granit.Invoicing.Invoice",
            r => r
                .DisplayAs(RelationDisplay.SmartButton)
                .DisplayKey("Invoicing:Relation.OnParty.Invoices")
                .Icon("file-text")
                .Order(10)
                .RequiresPermission(InvoicingPermissions.Invoices.Read)
                .Aggregate(a => a
                    .Count(labelKey: "Invoicing:Relation.OnParty.Invoices.Count")
                    .Sum(i => i.Total, labelKey: "Invoicing:Relation.OnParty.Invoices.Total", format: "currency")));
}
