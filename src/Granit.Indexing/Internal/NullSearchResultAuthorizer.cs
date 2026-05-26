namespace Granit.Indexing.Internal;

/// <summary>
/// Null-object <see cref="ISearchResultAuthorizer{TKey}"/> that authorises every
/// candidate. Registered by <c>AddGranitIndexing</c> as the default; consumers with
/// per-resource ACL register their own implementation explicitly.
/// </summary>
internal sealed class NullSearchResultAuthorizer<TKey> : ISearchResultAuthorizer<TKey>
{
    /// <inheritdoc/>
    public int RecommendedInitialMultiplier => 1;

    /// <inheritdoc/>
    public Task<AuthorizedResult<TKey>> FilterAsync(
        IReadOnlyList<TKey> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return Task.FromResult(new AuthorizedResult<TKey>(candidates));
    }
}
