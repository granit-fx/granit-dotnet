using Granit.AI.Chat.Settings;
using Granit.Settings.Definitions;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class AIChatSettingDefinitionProviderTests
{
    private sealed class CapturingContext : ISettingDefinitionContext
    {
        public Dictionary<string, SettingDefinition> Definitions { get; } = new(StringComparer.Ordinal);

        public void Add(SettingDefinition definition) => Definitions[definition.Name] = definition;

        public SettingDefinition? GetOrNull(string name) =>
            Definitions.GetValueOrDefault(name);
    }

    private static CapturingContext Define()
    {
        CapturingContext context = new();
        new AIChatSettingDefinitionProvider().Define(context);
        return context;
    }

    [Fact]
    public void Declares_the_three_user_scoped_chat_settings()
    {
        CapturingContext context = Define();

        context.Definitions.Keys.ShouldBe(
            [
                AIChatSettingNames.DefaultWorkspace,
                AIChatSettingNames.WebSearchPolicy,
                AIChatSettingNames.CustomContext,
            ],
            ignoreOrder: true);

        foreach (SettingDefinition definition in context.Definitions.Values)
        {
            definition.IsVisibleToClients.ShouldBeTrue();
            definition.Providers.ShouldBe(["U"]);
        }
    }

    [Fact]
    public void Web_search_policy_is_constrained_to_the_three_policies_with_a_safe_default()
    {
        SettingDefinition definition = Define().Definitions[AIChatSettingNames.WebSearchPolicy];

        definition.DefaultValue.ShouldBe(nameof(ChatWebSearchPolicy.Deny));
        definition.AllowedValues.ShouldBe(
            [nameof(ChatWebSearchPolicy.Deny), nameof(ChatWebSearchPolicy.Allow), nameof(ChatWebSearchPolicy.AlwaysAsk)],
            ignoreOrder: true);

        definition.IsValidValue("Allow").ShouldBeTrue();
        definition.IsValidValue("Sometimes").ShouldBeFalse();
    }

    [Fact]
    public void Custom_context_enforces_the_max_length()
    {
        SettingDefinition definition = Define().Definitions[AIChatSettingNames.CustomContext];

        definition.MaxLength.ShouldBe(AIChatSettingNames.MaxCustomContextLength);
        definition.IsValidValue(new string('x', AIChatSettingNames.MaxCustomContextLength)).ShouldBeTrue();
        definition.IsValidValue(new string('x', AIChatSettingNames.MaxCustomContextLength + 1)).ShouldBeFalse();
    }
}
