namespace Granit.Mentions;

/// <summary>
/// Decides whether the current caller may use a given <see cref="IMentionResolver"/>, enforcing its
/// <see cref="IMentionResolver.RequiredPermission"/>. Shared by every consumer of the mention seam
/// (the picker facade and AI chat's per-turn resolution) so authorization lives at the caller, not
/// in the resolver or in any directory reader.
/// </summary>
public interface IMentionAuthorizer
{
    /// <summary>Returns whether the current caller may search/resolve <paramref name="resolver"/>'s type.</summary>
    /// <param name="resolver">The resolver whose access is being checked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> IsAuthorizedAsync(IMentionResolver resolver, CancellationToken cancellationToken = default);
}
