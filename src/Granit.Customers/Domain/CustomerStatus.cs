namespace Granit.Customers.Domain;

/// <summary>Lifecycle status of a <see cref="Customer"/> aggregate.</summary>
/// <remarks>
/// <para>Allowed transitions:</para>
/// <list type="bullet">
/// <item><see cref="Active"/> ↔ <see cref="Suspended"/> (e.g., temporary block for unpaid balance)</item>
/// <item><see cref="Active"/> → <see cref="Archived"/> (terminal — kept for accounting integrity)</item>
/// <item><see cref="Suspended"/> → <see cref="Archived"/></item>
/// </list>
/// <para>
/// <see cref="Archived"/> is terminal: archived customers cannot be reactivated and can no longer
/// be invoiced or subscribed. Their row is preserved (legal retention) — pseudonymisation is the
/// DPO/Privacy module's responsibility.
/// </para>
/// </remarks>
public enum CustomerStatus
{
    /// <summary>Default state on creation. Eligible for invoicing, subscriptions, and payments.</summary>
    Active = 0,

    /// <summary>Temporarily blocked (e.g., unpaid balance, fraud review). Restore via <c>Activate()</c>.</summary>
    Suspended = 1,

    /// <summary>Terminal state. Customer is no longer transactable but row is retained.</summary>
    Archived = 2,
}
