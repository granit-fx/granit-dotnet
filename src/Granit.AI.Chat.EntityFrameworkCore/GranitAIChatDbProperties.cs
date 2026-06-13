using Granit.Persistence.EntityFrameworkCore;

namespace Granit.AI.Chat.EntityFrameworkCore;

/// <summary>
/// Configurable table-naming properties for the AI Chat EF Core module. Set at startup, before
/// the model is first built (EF Core caches the compiled model).
/// </summary>
public static class GranitAIChatDbProperties
{
    /// <summary>Table name prefix for all chat tables. Default: <c>"ai_chat_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "ai_chat_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema. Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>, then
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
