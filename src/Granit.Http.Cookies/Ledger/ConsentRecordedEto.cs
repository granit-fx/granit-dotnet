using Granit.Events;
using Granit.Http.Cookies.Domain;

namespace Granit.Http.Cookies.Ledger;

/// <summary>
/// Published when a cookie-consent decision has been durably recorded in the consent
/// ledger. Enables downstream accountability tooling (consent statistics, compliance
/// dashboards, SIEM feeds) without re-reading the ledger table.
/// </summary>
/// <remarks>
/// Flat serializable snapshot of the persisted <see cref="CookieConsentRecord"/>.
/// Dispatched by the persistent ledger implementation <em>after</em> the save succeeds
/// (mirrors the Auditing pipeline rule: never an event for a row that failed to persist);
/// the no-op <c>NullConsentLedger</c> records nothing and therefore publishes nothing.
/// </remarks>
/// <param name="RecordId">Identifier of the persisted consent record.</param>
/// <param name="TenantId">Tenant identifier (null for host-level decisions).</param>
/// <param name="GrantedCategories">Snake_case names of the granted categories.</param>
/// <param name="DeniedCategories">Snake_case names of the denied categories.</param>
/// <param name="Mode">Consent model name (e.g. <c>"OptIn"</c>).</param>
/// <param name="DecidedAt">When the user took the decision (UTC).</param>
public sealed record ConsentRecordedEto(
    Guid RecordId,
    Guid? TenantId,
    IReadOnlyList<string> GrantedCategories,
    IReadOnlyList<string> DeniedCategories,
    string Mode,
    DateTimeOffset DecidedAt) : IIntegrationEvent;
