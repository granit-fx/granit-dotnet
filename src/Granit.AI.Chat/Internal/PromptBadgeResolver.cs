using Granit.AI.Prompts;
using Granit.AI.Prompts.Domain;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// The outcome of expanding a turn's <c>/</c> prompt badges: the composed instruction plus the
/// primary referenced prompt's identity for usage stamping.
/// </summary>
internal sealed record PromptBadgeResolution
{
    /// <summary>The referenced prompt contents composed with the user's free text.</summary>
    public required string ComposedMessage { get; init; }

    /// <summary>The first resolved prompt's name, or <see langword="null"/> when none resolved.</summary>
    public string? PrimaryPromptName { get; init; }

    /// <summary>The first resolved prompt's version, or <see langword="null"/> when none resolved.</summary>
    public int? PrimaryPromptVersion { get; init; }
}

/// <summary>
/// Expands the catalogue prompt templates referenced as <c>/</c> badges (ADR-067) and composes them
/// with the user's free text into the final instruction. Resolution is owner-scoped (system prompts
/// plus the caller's own); references that resolve to nothing (deleted or another user's private
/// prompt) are silently dropped, never leaked.
/// </summary>
internal interface IPromptBadgeResolver
{
    Task<PromptBadgeResolution> ResolveAsync(
        IReadOnlyList<Guid> promptRefs, Guid ownerId, string message, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IPromptBadgeResolver"/>
internal sealed class PromptBadgeResolver(IPromptTemplateStore store) : IPromptBadgeResolver
{
    public async Task<PromptBadgeResolution> ResolveAsync(
        IReadOnlyList<Guid> promptRefs, Guid ownerId, string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promptRefs);

        List<PromptTemplate> resolved = [];
        foreach (Guid id in promptRefs.Distinct())
        {
            PromptTemplate? prompt = await store.GetAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
            if (prompt is not null)
            {
                resolved.Add(prompt);
            }
        }

        if (resolved.Count == 0)
        {
            return new PromptBadgeResolution { ComposedMessage = message };
        }

        IEnumerable<string> parts = resolved
            .Select(p => p.Content)
            .Append(message)
            .Where(part => !string.IsNullOrWhiteSpace(part));

        PromptTemplate primary = resolved[0];
        return new PromptBadgeResolution
        {
            ComposedMessage = string.Join("\n\n", parts),
            PrimaryPromptName = primary.Name,
            PrimaryPromptVersion = primary.Version,
        };
    }
}
