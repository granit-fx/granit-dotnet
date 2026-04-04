using Granit.Guids;
using Granit.Payments.Domain;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.EntityFrameworkCore.Internal;

/// <summary>
/// Insert-first webhook dedup store. Catches unique constraint violation for duplicates.
/// </summary>
internal sealed partial class EfProcessedWebhookEventStore(
    IDbContextFactory<PaymentsDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILogger<EfProcessedWebhookEventStore> logger) : IProcessedWebhookEventStore
{
    public async Task<bool> TryRecordAsync(
        string providerName, string providerEventId, string eventType,
        CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = ProcessedWebhookEvent.Create(
            guidGenerator.Create(), providerName, providerEventId, eventType, clock.Now);

        try
        {
            db.ProcessedWebhookEvents.Add(record);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            Log.WebhookDuplicate(logger, providerName, providerEventId);
            return false;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Webhook duplicate detected: {ProviderName}/{ProviderEventId}")]
        public static partial void WebhookDuplicate(ILogger logger, string providerName, string providerEventId);
    }
}
