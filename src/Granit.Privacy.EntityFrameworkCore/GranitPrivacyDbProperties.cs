namespace Granit.Privacy.EntityFrameworkCore;

/// <summary>Table-naming properties for the Privacy EF Core module.</summary>
public static class GranitPrivacyDbProperties
{
    /// <summary>Table prefix. Default: <c>"privacy_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "privacy_";

    /// <summary>Schema. Default: <c>null</c> (uses database default).</summary>
    public static string? DbSchema { get; set; }
}
