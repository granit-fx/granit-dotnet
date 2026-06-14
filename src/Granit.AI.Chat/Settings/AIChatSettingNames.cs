namespace Granit.AI.Chat.Settings;

/// <summary>
/// Canonical names of the per-user chat settings (ADR-067), declared by
/// <c>AIChatSettingDefinitionProvider</c> on the User (<c>"U"</c>) scope. Consumers should
/// reference these constants rather than literal strings.
/// </summary>
public static class AIChatSettingNames
{
    private const string Prefix = "Granit.AI.Chat.";

    /// <summary>The user's default chat workspace, or <see cref="ReservedAutoWorkspace"/> for automatic routing.</summary>
    public const string DefaultWorkspace = Prefix + "DefaultWorkspace";

    /// <summary>The user's web-search policy (a <see cref="ChatWebSearchPolicy"/> name).</summary>
    public const string WebSearchPolicy = Prefix + "WebSearchPolicy";

    /// <summary>The user's free-text custom context, layered into the system prompt.</summary>
    public const string CustomContext = Prefix + "CustomContext";

    /// <summary>
    /// Reserved <see cref="DefaultWorkspace"/> value meaning "let the system choose". Automatic
    /// routing is phase 2; until then it resolves to the configured default workspace.
    /// </summary>
    public const string ReservedAutoWorkspace = "Auto";

    /// <summary>Maximum length, in characters, of <see cref="CustomContext"/>.</summary>
    public const int MaxCustomContextLength = 4000;
}
