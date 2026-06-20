namespace Granit.Mentions;

/// <summary>
/// Application-implemented seam for one mentionable entity <see cref="Type"/>: it searches
/// candidates for the <c>@</c> picker and resolves a chosen reference to a <see cref="MentionTarget"/>.
/// Domain-neutral — consumed by AI chat (which injects the target as untrusted LLM context),
/// Timeline, and any other feature. Opt-in and per-scope, so it runs under the caller's identity
/// and ACLs.
/// </summary>
/// <remarks>
/// A resolver MUST never surface data the caller could not read: <see cref="SearchAsync"/> returns
/// only visible candidates and <see cref="ResolveAsync"/> returns <see langword="null"/> when the
/// entity is absent or forbidden. Coarse access is gated by <see cref="RequiredPermission"/>; the
/// resolver remains free of any authorization dependency.
/// </remarks>
public interface IMentionResolver
{
    /// <summary>
    /// The mention type this resolver handles (case-insensitive, unique across resolvers),
    /// e.g. <c>user</c>, <c>invoice</c>.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// The permission a caller must hold to search or resolve this type, or <see langword="null"/>
    /// when it is available to any caller who may use mentions (the default). Enforced by the host,
    /// not by the resolver.
    /// </summary>
    string? RequiredPermission => null;

    /// <summary>
    /// Searches the entities of this <see cref="Type"/> the caller may mention.
    /// </summary>
    /// <param name="query">The user's free-text query (may be empty for an initial list).</param>
    /// <param name="limit">The maximum number of suggestions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Up to <paramref name="limit"/> suggestions; empty when nothing matches. Never <see langword="null"/>.</returns>
    ValueTask<IReadOnlyList<MentionSuggestion>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a referenced entity to its target, under the caller's ACLs.
    /// </summary>
    /// <param name="id">The opaque identifier from the mention.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved target, or <see langword="null"/> when absent or the caller may not see it.</returns>
    ValueTask<MentionTarget?> ResolveAsync(string id, CancellationToken cancellationToken = default);
}
