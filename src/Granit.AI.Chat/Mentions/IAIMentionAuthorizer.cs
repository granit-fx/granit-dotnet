namespace Granit.AI.Chat.Mentions;

/// <summary>
/// Decides whether the current caller may use a given <see cref="IAIMentionResolver"/>, enforcing
/// its <see cref="IAIMentionResolver.RequiredPermission"/> (ADR-067). This is the authorization
/// boundary for the <c>@</c> picker: the search dispatcher consults it before querying any resolver,
/// so a type the caller cannot read is never searched and never leaks suggestions.
/// </summary>
/// <remarks>
/// The base package registers a permissive default (everything allowed); the host layer that owns
/// the permission system (<c>Granit.AI.Chat.Endpoints</c>) replaces it with a checker bound to the
/// caller's grants. Keeping the check here — not in the resolver or the directory reader — lets
/// resolvers stay authorization-agnostic and the reader stay usable from system contexts.
/// </remarks>
public interface IAIMentionAuthorizer
{
    /// <summary>
    /// Returns whether the current caller may use <paramref name="resolver"/>.
    /// </summary>
    /// <param name="resolver">The resolver whose access is being checked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the caller is allowed to search/resolve this type.</returns>
    ValueTask<bool> IsAuthorizedAsync(IAIMentionResolver resolver, CancellationToken cancellationToken = default);
}
