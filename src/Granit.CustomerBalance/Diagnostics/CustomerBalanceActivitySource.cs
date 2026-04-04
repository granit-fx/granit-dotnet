using System.Diagnostics;

namespace Granit.CustomerBalance.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.CustomerBalance distributed tracing.
/// </summary>
internal static class CustomerBalanceActivitySource
{
    internal const string Name = "Granit.CustomerBalance";

    internal static readonly ActivitySource Source = new(Name);

    internal const string CreditBalance = "customer_balance.credit";
    internal const string DebitBalance = "customer_balance.debit";
    internal const string ExpireCredit = "customer_balance.expire_credit";
}
