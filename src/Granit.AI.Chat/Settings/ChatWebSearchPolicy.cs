namespace Granit.AI.Chat.Settings;

/// <summary>
/// The user's policy for the chat agent using web search (ADR-067). The web-search provider
/// itself is deferred to phase 2; this setting captures the user's intent in advance.
/// </summary>
public enum ChatWebSearchPolicy
{
    /// <summary>The agent must not search the web.</summary>
    Deny,

    /// <summary>The agent may search the web without asking.</summary>
    Allow,

    /// <summary>The agent must ask for confirmation before searching the web.</summary>
    AlwaysAsk,
}
