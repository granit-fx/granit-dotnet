using Granit.Settings.Definitions;

namespace Granit.AI.Chat.Settings;

/// <summary>
/// Declares the per-user chat settings (ADR-067): default workspace, web-search policy and a
/// free-text custom context. All settable at User scope and visible to clients (the settings UI
/// reads/writes them via the generic <c>Granit.Settings.Endpoints</c> user endpoints).
/// Auto-discovered by <c>GranitSettingsModule</c> — no manual registration needed.
/// </summary>
internal sealed class AIChatSettingDefinitionProvider : ISettingDefinitionProvider
{
    public void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(AIChatSettingNames.DefaultWorkspace)
        {
            IsVisibleToClients = true,
            DisplayName = "Default chat workspace",
            Description = "The chat-capable workspace used by default for your conversations, or "
                + "'Auto' to let the system choose. The selectable list is served by the chat "
                + "workspaces endpoint (chat-capable workspaces only).",
            Providers = { "U" },
        });

        context.Add(new SettingDefinition(AIChatSettingNames.WebSearchPolicy)
        {
            IsVisibleToClients = true,
            DisplayName = "Web-search policy",
            Description = "Whether the chat agent may search the web: Deny, Allow, or AlwaysAsk "
                + "(ask for confirmation first). The web-search provider is deferred to phase 2.",
            Providers = { "U" },
            DefaultValue = nameof(ChatWebSearchPolicy.Deny),
            AllowedValues =
            [
                nameof(ChatWebSearchPolicy.Deny),
                nameof(ChatWebSearchPolicy.Allow),
                nameof(ChatWebSearchPolicy.AlwaysAsk),
            ],
        });

        context.Add(new SettingDefinition(AIChatSettingNames.CustomContext)
        {
            IsVisibleToClients = true,
            DisplayName = "Custom context",
            Description = "Free-text context layered into the system prompt for your conversations. "
                + "It refines behaviour but never overrides the framework guardrails.",
            Providers = { "U" },
            MaxLength = AIChatSettingNames.MaxCustomContextLength,
        });
    }
}
