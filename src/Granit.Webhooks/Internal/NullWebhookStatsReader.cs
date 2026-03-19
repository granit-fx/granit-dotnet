using Granit.Webhooks.Abstractions;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Default no-op implementation of <see cref="IWebhookStatsReader"/>.
/// Returns zero values. Replaced by <c>EfWebhookStatsReader</c> when
/// <c>Granit.Webhooks.EntityFrameworkCore</c> is loaded.
/// </summary>
internal sealed class NullWebhookStatsReader : IWebhookStatsReader
{
    public Task<WebhookStats> GetStatsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebhookStats(0, 0, 0, 0, 0, 0.0, 0.0));
}
