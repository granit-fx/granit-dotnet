namespace Granit.AI.Chat.Mentions;

/// <summary>
/// Application-implemented seam that resolves a single <c>@</c> mention of one
/// <see cref="Type"/> to <see cref="AIMentionContext"/> (ADR-067). Mentions reuse the same
/// opt-in, ACL-bound model as tools: a resolver is exposed only by explicit application
/// registration (<c>AddGranitChatMentions</c>) and is resolved per scope so it runs under the
/// calling user's identity and ACLs.
/// </summary>
/// <remarks>
/// A resolver MUST never surface data the caller could not read. When the entity does not
/// exist, or the caller is not allowed to see it, return <see langword="null"/>: the mention
/// is then silently dropped and nothing about it enters the prompt. Returning a placeholder
/// "not found" string would itself leak the entity's existence — return <see langword="null"/>.
/// </remarks>
public interface IAIMentionResolver
{
    /// <summary>
    /// The mention type this resolver handles, matched against <see cref="AIMention.Type"/>
    /// (case-insensitive). Unique across opted-in resolvers, e.g. <c>invoice</c>, <c>user</c>.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Resolves the referenced entity to context, under the caller's ACLs.
    /// </summary>
    /// <param name="id">The opaque identifier from the mention.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The resolved context, or <see langword="null"/> when the entity is absent or the caller
    /// may not see it — in which case the mention is dropped and never leaked into the prompt.
    /// </returns>
    ValueTask<AIMentionContext?> ResolveAsync(string id, CancellationToken cancellationToken = default);
}
