namespace Granit.AI.Prompts.Seeding;

/// <summary>
/// The framework-shipped generic prompts seeded into every tenant's catalogue under the
/// <see cref="Domain.PromptCategory.GeneralName"/> category (ADR-067). Business-domain prompts are
/// seeded separately by <c>granit-business</c>.
/// </summary>
public static class GenericPrompts
{
    /// <summary>The generic prompts, keyed by stable <see cref="GenericPromptSeed.NameKey"/>.</summary>
    public static IReadOnlyList<GenericPromptSeed> All { get; } =
    [
        new(
            "Prompt:Summarize:Name",
            "Prompt:Summarize:Description",
            "Summarise the selected content clearly and concisely, preserving the key points.",
            "sparkles",
            "#8B5CF6"),
        new(
            "Prompt:Draft:Name",
            "Prompt:Draft:Description",
            "Draft a clear, well-structured response based on the context.",
            "pencil",
            "#10B981"),
        new(
            "Prompt:DailyBrief:Name",
            "Prompt:DailyBrief:Description",
            "Give me a concise brief of what needs my attention today.",
            "sun",
            "#F59E0B"),
        new(
            "Prompt:FindRelated:Name",
            "Prompt:FindRelated:Description",
            "Find and list items related to the current context.",
            "link",
            "#3B82F6"),
    ];
}
