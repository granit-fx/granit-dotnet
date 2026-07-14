using Granit.Events;
using Granit.Http.Cookies.Domain;
using Granit.Http.Cookies.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Granit.Http.Cookies.EntityFrameworkCore.Internal;

/// <summary>
/// Durable, append-only EF Core implementation of <see cref="IConsentLedger"/>.
/// </summary>
/// <remarks>
/// Mirrors the Auditing standalone persistence pipeline: the row is the source of truth,
/// and the <see cref="ConsentRecordedEto"/> is dispatched through
/// <see cref="IIntegrationEventDispatcher"/> <em>after</em> the save succeeds — never an
/// event for a row that failed to persist. Records are inserted only; updates and deletes
/// go exclusively through the GDPR erasure primitive (<see cref="ICookieConsentEraser"/>).
/// </remarks>
internal sealed class EfCoreConsentLedger(
    IDbContextFactory<CookiesDbContext> contextFactory,
    IIntegrationEventDispatcher integrationEventDispatcher) : IConsentLedger
{
    /// <inheritdoc/>
    public async Task RecordAsync(CookieConsentRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using CookiesDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        context.ConsentRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Dispatch after the save: never an event for a row that failed to persist.
        await integrationEventDispatcher
            .DispatchAsync([ToEto(record)], cancellationToken)
            .ConfigureAwait(false);
    }

    private static ConsentRecordedEto ToEto(CookieConsentRecord record) => new(
        record.Id,
        record.TenantId,
        record.GrantedCategories,
        record.DeniedCategories,
        record.Mode.ToString(),
        record.DecidedAt);
}
