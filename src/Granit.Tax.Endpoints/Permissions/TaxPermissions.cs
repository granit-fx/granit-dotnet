namespace Granit.Tax.Endpoints.Permissions;

/// <summary>Permission constants for Granit.Tax.Endpoints.</summary>
public static class TaxPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Tax";

    /// <summary>Tax rate resource permissions.</summary>
    public static class Rates
    {
        /// <summary>View tax rates.</summary>
        public const string Read = "Tax.Rates.Read";

        /// <summary>Manage tax rate overrides.</summary>
        public const string Manage = "Tax.Rates.Manage";
    }

    /// <summary>Tax ID validation permissions.</summary>
    public static class Validations
    {
        /// <summary>View cached validations.</summary>
        public const string Read = "Tax.Validations.Read";

        /// <summary>Execute tax ID validation.</summary>
        public const string Execute = "Tax.Validations.Execute";
    }
}
