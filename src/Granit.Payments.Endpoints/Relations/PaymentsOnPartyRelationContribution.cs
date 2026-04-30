using Granit.Entities.Relations;
using Granit.Parties.Domain;
using Granit.Payments.Domain;
using Granit.Payments.Endpoints.Permissions;

namespace Granit.Payments.Endpoints.Relations;

/// <summary>
/// Phase 1.F-β cobaye — grafts a <c>"payments"</c> smart-button relation
/// onto <c>Granit.Parties.Party</c>, surfacing <c>Count</c> + <c>Sum(Amount)</c>
/// on the party detail header.
/// </summary>
internal sealed class PaymentsOnPartyRelationContribution : IEntityRelationContributor
{
    public void Contribute(IEntityRelationContributionContext context) =>
        context.AddRelation<Party, PaymentTransaction>(
            name: "payments",
            targetEntityName: "Granit.Payments.PaymentTransaction",
            r => r
                .DisplayAs(RelationDisplay.SmartButton)
                .DisplayKey("Payments:Relation.OnParty.Payments")
                .Icon("credit-card")
                .Order(30)
                .RequiresPermission(PaymentsPermissions.Transactions.Read)
                .Aggregate(a => a
                    .Count(labelKey: "Payments:Relation.OnParty.Payments.Count")
                    .Sum(p => p.Amount, labelKey: "Payments:Relation.OnParty.Payments.Amount", format: "currency")));
}
