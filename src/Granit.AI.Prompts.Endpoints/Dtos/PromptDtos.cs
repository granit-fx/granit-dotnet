using Granit.AI.Prompts.Domain;

namespace Granit.AI.Prompts.Endpoints.Dtos;

/// <summary>Request to create a prompt in the catalogue.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Content">The prompt instruction text.</param>
/// <param name="ShortDescription">A one-line catalogue description.</param>
/// <param name="Icon">An icon identifier, or <see langword="null"/>.</param>
/// <param name="IconColor">The icon colour as <c>#RRGGBB</c>/<c>#RRGGBBAA</c>, or <see langword="null"/>.</param>
/// <param name="CategoryIds">The categories to file the prompt under, or <see langword="null"/>.</param>
public sealed record CreatePromptRequest(
    string Name,
    string Content,
    string? ShortDescription = null,
    string? Icon = null,
    string? IconColor = null,
    IReadOnlyList<Guid>? CategoryIds = null);

/// <summary>Request to update one of the caller's own prompts.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Content">The prompt instruction text.</param>
/// <param name="ShortDescription">A one-line catalogue description.</param>
/// <param name="Icon">An icon identifier, or <see langword="null"/>.</param>
/// <param name="IconColor">The icon colour as <c>#RRGGBB</c>/<c>#RRGGBBAA</c>, or <see langword="null"/>.</param>
/// <param name="CategoryIds">The categories to file the prompt under, or <see langword="null"/>.</param>
public sealed record UpdatePromptRequest(
    string Name,
    string Content,
    string? ShortDescription = null,
    string? Icon = null,
    string? IconColor = null,
    IReadOnlyList<Guid>? CategoryIds = null);

/// <summary>A prompt in a catalogue list (without its instruction text).</summary>
public sealed record PromptSummaryResponse(
    Guid Id,
    string Name,
    string ShortDescription,
    string? Icon,
    string? IconColor,
    bool IsSystem,
    IReadOnlyList<Guid> CategoryIds)
{
    /// <summary>Projects an aggregate to a summary, using the resolved display name/description.</summary>
    public static PromptSummaryResponse FromAggregate(PromptTemplate prompt, string name, string shortDescription) =>
        new(prompt.Id, name, shortDescription, prompt.Icon, prompt.IconColor?.Value, prompt.IsSystem,
            [.. prompt.CategoryLinks.Select(l => l.CategoryId)]);
}

/// <summary>A full prompt, including its instruction text.</summary>
public sealed record PromptResponse(
    Guid Id,
    string Name,
    string ShortDescription,
    string Content,
    string? Icon,
    string? IconColor,
    int Version,
    bool IsSystem,
    Guid OwnerId,
    IReadOnlyList<Guid> CategoryIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt)
{
    /// <summary>Projects an aggregate to a response, using the resolved display name/description.</summary>
    public static PromptResponse FromAggregate(PromptTemplate prompt, string name, string shortDescription) =>
        new(prompt.Id, name, shortDescription, prompt.Content, prompt.Icon, prompt.IconColor?.Value,
            prompt.Version, prompt.IsSystem, prompt.OwnerId,
            [.. prompt.CategoryLinks.Select(l => l.CategoryId)],
            prompt.CreatedAt, prompt.ModifiedAt);
}

/// <summary>The catalogue grouped by category for the chat <c>/</c> picker.</summary>
public sealed record PromptPickerResponse(IReadOnlyList<PromptPickerCategoryResponse> Categories);

/// <summary>A category group in the picker.</summary>
/// <param name="CategoryId">The category id, or <see langword="null"/> for the implicit "General" group.</param>
/// <param name="CategoryName">The category display name.</param>
/// <param name="Prompts">The prompts in this group, system prompts first then by name.</param>
public sealed record PromptPickerCategoryResponse(
    Guid? CategoryId,
    string CategoryName,
    IReadOnlyList<PromptPickerItemResponse> Prompts);

/// <summary>A prompt as shown in the picker (decoration plus a short description, no instruction text).</summary>
public sealed record PromptPickerItemResponse(
    Guid Id,
    string Name,
    string ShortDescription,
    string? Icon,
    string? IconColor,
    bool IsSystem);
