using Granit.Domain.ValueObjects;

namespace Granit.AI.Prompts;

/// <summary>
/// The editable shape of a <see cref="Domain.PromptTemplate"/> — the fields a user may change on
/// their own prompt. Passed to <see cref="IPromptTemplateStore.UpdateAsync"/>; category membership
/// is reconciled to <see cref="CategoryIds"/> (a prompt may sit in several categories).
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="ShortDescription">One-line catalogue description.</param>
/// <param name="Content">The prompt instruction text.</param>
/// <param name="Icon">An icon identifier, or <see langword="null"/>.</param>
/// <param name="IconColor">The icon colour, or <see langword="null"/>.</param>
/// <param name="CategoryIds">The categories the prompt belongs to (membership is replaced wholesale).</param>
public sealed record PromptTemplateEdit(
    string Name,
    string ShortDescription,
    string Content,
    string? Icon,
    HexColor? IconColor,
    IReadOnlyList<Guid> CategoryIds);
