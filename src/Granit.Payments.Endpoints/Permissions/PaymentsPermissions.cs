namespace Granit.Payments.Endpoints.Permissions;

/// <summary>Permission constants for Granit.Payments.Endpoints.</summary>
public static class PaymentsPermissions
{
    public const string GroupName = "Payments";

    public static class Transactions
    {
        public const string Read = "Payments.Transactions.Read";
    }

    public static class Charges
    {
        public const string Execute = "Payments.Charges.Execute";
    }

    public static class Refunds
    {
        public const string Execute = "Payments.Refunds.Execute";
    }

    public static class Methods
    {
        public const string Read = "Payments.Methods.Read";
        public const string Manage = "Payments.Methods.Manage";
    }
}
