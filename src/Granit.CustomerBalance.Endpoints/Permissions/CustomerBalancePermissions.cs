namespace Granit.CustomerBalance.Endpoints.Permissions;

/// <summary>Permission constants for Granit.CustomerBalance.Endpoints.</summary>
public static class CustomerBalancePermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "CustomerBalance";

    /// <summary>Balance account resource permissions.</summary>
    public static class Accounts
    {
        /// <summary>View balance accounts.</summary>
        public const string Read = "CustomerBalance.Accounts.Read";
    }

    /// <summary>Balance transaction resource permissions.</summary>
    public static class Transactions
    {
        /// <summary>View transaction history.</summary>
        public const string Read = "CustomerBalance.Transactions.Read";
    }

    /// <summary>Admin credit management permissions.</summary>
    public static class Credits
    {
        /// <summary>Manage credits (manual credit, adjustment).</summary>
        public const string Manage = "CustomerBalance.Credits.Manage";
    }
}
