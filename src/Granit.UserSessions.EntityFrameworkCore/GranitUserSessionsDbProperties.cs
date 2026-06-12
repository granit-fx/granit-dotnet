namespace Granit.UserSessions.EntityFrameworkCore;

/// <summary>
/// Database naming properties for the user-sessions EF Core store. Set before the first DbContext is created.
/// </summary>
public static class GranitUserSessionsDbProperties
{
    /// <summary>Table name prefix. Default: <c>"user_sessions_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "user_sessions_";

    /// <summary>Optional schema. <c>null</c> uses the provider default.</summary>
    public static string? DbSchema { get; set; }
}
