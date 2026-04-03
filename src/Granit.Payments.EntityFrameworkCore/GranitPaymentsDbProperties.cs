namespace Granit.Payments.EntityFrameworkCore;

/// <summary>Table-naming properties for Payments EF Core module.</summary>
public static class GranitPaymentsDbProperties
{
    public static string DbTablePrefix { get; set; } = "payments_";
    public static string? DbSchema { get; set; }
}
