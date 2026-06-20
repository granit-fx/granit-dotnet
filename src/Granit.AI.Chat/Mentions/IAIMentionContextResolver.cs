namespace Granit.AI.Chat.Mentions;

/// <summary>
/// Resolves the <c>@</c> mentions on a turn to a single context block to inject ahead of the
/// user's message. Each mention is resolved through the <c>Granit.Mentions</c> picker facade
/// under the caller's ACLs; unknown types and mentions the caller cannot see are dropped, and
/// every resolved entity is wrapped in the untrusted-data envelope.
/// </summary>
public interface IAIMentionContextResolver
{
    /// <summary>
    /// Resolves <paramref name="mentions"/> to an injectable context block.
    /// </summary>
    /// <param name="mentions">The mentions carried on the turn.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The context block to prepend to the user's message, or <see langword="null"/> when no
    /// mention resolved (none supplied, unknown types only, or all denied by ACLs).
    /// </returns>
    ValueTask<string?> ResolveContextAsync(
        IReadOnlyList<AIMention> mentions,
        CancellationToken cancellationToken = default);
}
