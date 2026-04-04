namespace Granit.CustomerBalance.EntityFrameworkCore;

/// <summary>Database table naming properties for the CustomerBalance module.</summary>
public static class GranitCustomerBalanceDbProperties
{
    /// <summary>Table prefix. Default: <c>"customer_balance_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "customer_balance_";

    /// <summary>Schema. Default: <c>null</c>.</summary>
    public static string? DbSchema { get; set; }
}
