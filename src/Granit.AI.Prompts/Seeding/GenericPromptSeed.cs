namespace Granit.AI.Prompts.Seeding;

/// <summary>
/// Definition of a framework-seeded generic prompt (ADR-067). <see cref="NameKey"/> and
/// <see cref="DescriptionKey"/> are <c>AIPrompts</c> localization keys (resolved to the request
/// culture by the catalogue endpoint); <see cref="Content"/> is the instruction sent to the model.
/// </summary>
/// <param name="NameKey">Localization key for the display name.</param>
/// <param name="DescriptionKey">Localization key for the short description.</param>
/// <param name="Content">The prompt instruction.</param>
/// <param name="Icon">Catalogue icon identifier.</param>
/// <param name="IconColor">Catalogue icon colour (hex).</param>
public sealed record GenericPromptSeed(
    string NameKey,
    string DescriptionKey,
    string Content,
    string Icon,
    string IconColor);
