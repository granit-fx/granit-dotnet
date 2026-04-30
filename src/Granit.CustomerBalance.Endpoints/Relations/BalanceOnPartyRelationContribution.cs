using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Endpoints.Permissions;
using Granit.Entities.Relations;
using Granit.Parties.Domain;

namespace Granit.CustomerBalance.Endpoints.Relations;

/// <summary>
/// Phase 1.F-β cobaye — grafts a <c>"balance"</c> smart-button relation
/// onto <c>Granit.Parties.Party</c>, surfacing the running customer balance
/// (<c>Sum(Balance)</c> over zero-or-one BalanceAccount per party).
/// </summary>
/// <remarks>
/// Modelled as a 1:N relation by the cross-module contributor surface even
/// though the source's cardinality is at most 1 — the framework's
/// <c>IEntityRelationContributionContext.AddRelation&lt;TSource,TRelated&gt;</c>
/// hardcodes <see cref="RelationCardinality.Many"/>; the renderer collapses
/// the count-of-1 case into the single-value smart button.
/// </remarks>
internal sealed class BalanceOnPartyRelationContribution : IEntityRelationContributor
{
    public void Contribute(IEntityRelationContributionContext context) =>
        context.AddRelation<Party, BalanceAccount>(
            name: "balance",
            targetEntityName: "Granit.CustomerBalance.BalanceAccount",
            r => r
                .DisplayAs(RelationDisplay.SmartButton)
                .DisplayKey("CustomerBalance:Relation.OnParty.Balance")
                .Icon("wallet")
                .Order(40)
                .RequiresPermission(CustomerBalancePermissions.Accounts.Read)
                .Aggregate(a => a
                    .Sum(b => b.Balance, labelKey: "CustomerBalance:Relation.OnParty.Balance.Current", format: "currency")));
}
