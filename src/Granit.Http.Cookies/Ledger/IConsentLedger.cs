using Granit.Http.Cookies.Domain;

namespace Granit.Http.Cookies.Ledger;

/// <summary>
/// Append-only write path for cookie-consent decisions (GDPR Art. 7(1) accountability).
/// </summary>
/// <remarks>
/// The default registration is a no-op (<c>NullConsentLedger</c>) so the consent capture
/// endpoint works without persistence. Add <c>Granit.Http.Cookies.EntityFrameworkCore</c>
/// and call <c>AddGranitCookiesEntityFrameworkCore</c> for a durable ledger; the EF Core
/// implementation additionally dispatches a <see cref="ConsentRecordedEto"/> after each
/// successful write — never an event for a row that failed to persist.
/// </remarks>
public interface IConsentLedger
{
    /// <summary>Appends a consent decision to the ledger.</summary>
    /// <param name="record">The immutable consent record to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(CookieConsentRecord record, CancellationToken cancellationToken = default);
}
