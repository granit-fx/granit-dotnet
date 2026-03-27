namespace Granit.AI;

/// <summary>
/// Checks whether the current tenant has remaining AI quota before making an LLM call.
/// </summary>
/// <remarks>
/// Default implementation: <see cref="Internal.NullAIQuotaGuard"/> (no limits).
/// Configure limits via <c>AI:Quota</c> in <c>appsettings.json</c> and register the
/// built-in <see cref="Internal.InMemoryAIQuotaGuard"/> for enforcement.
/// </remarks>
public interface IAIQuotaGuard
{
    /// <summary>
    /// Checks whether the current tenant may proceed with an AI call.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AIQuotaResult"/> indicating whether the call is allowed.</returns>
    Task<AIQuotaResult> CheckAsync(CancellationToken cancellationToken = default);
}
