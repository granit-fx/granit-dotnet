using Granit.Entities.Relations;
using Granit.Parties.Domain;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Endpoints.Permissions;

namespace Granit.Subscriptions.Endpoints.Relations;

/// <summary>
/// Phase 1.F-β cobaye — grafts a <c>"subscriptions"</c> smart-button
/// relation onto <c>Granit.Parties.Party</c>, surfacing the count of
/// subscriptions on the party detail header.
/// </summary>
internal sealed class SubscriptionsOnPartyRelationContribution : IEntityRelationContributor
{
    public void Contribute(IEntityRelationContributionContext context) =>
        context.AddRelation<Party, Subscription>(
            name: "subscriptions",
            targetEntityName: "Granit.Subscriptions.Subscription",
            r => r
                .DisplayAs(RelationDisplay.SmartButton)
                .DisplayKey("Subscriptions:Relation.OnParty.Subscriptions")
                .Icon("repeat")
                .Order(20)
                .RequiresPermission(SubscriptionsPermissions.Subscriptions.Read)
                .Aggregate(a => a
                    .Count(labelKey: "Subscriptions:Relation.OnParty.Subscriptions.Count")));
}
