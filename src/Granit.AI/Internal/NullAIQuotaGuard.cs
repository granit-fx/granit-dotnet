namespace Granit.AI.Internal;

/// <summary>
/// No-op quota guard that allows all AI calls (no limits).
/// </summary>
internal sealed class NullAIQuotaGuard : IAIQuotaGuard
{
    public Task<AIQuotaResult> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AIQuotaResult.Allowed);
}
