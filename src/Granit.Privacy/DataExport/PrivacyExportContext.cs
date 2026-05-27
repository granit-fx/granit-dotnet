namespace Granit.Privacy.DataExport;

/// <summary>
/// Context object passed to <see cref="IPrivacyDataProvider"/> calls for the Takeout-style
/// portability flow. Replaces the prior bare <c>(userId, ct)</c> tuple to make tenant scope,
/// caller identity, and replay context explicit.
/// </summary>
/// <param name="RequestId">Correlation id of the export request — also the saga key.</param>
/// <param name="SubjectUserId">Data subject whose data is being exported (GDPR Art. 4(1)).</param>
/// <param name="CallerUserId">Identity that initiated the request. Equals <see cref="SubjectUserId"/>
/// for self-service exports (<c>Privacy.Exports.Execute</c>); differs for admin DSR flows
/// (<c>Privacy.Exports.ExecuteOnBehalfOf</c>).</param>
/// <param name="TenantId">Tenant scope the request runs under. <see langword="null"/> when the
/// host has no multi-tenancy. Background handlers MUST open
/// <c>ICurrentTenant.Change(TenantId)</c> before resolving scoped services.</param>
/// <param name="Regulation">Privacy regulation code (e.g. <c>EU_GDPR</c>, <c>BR_LGPD</c>).</param>
public sealed record PrivacyExportContext(
    Guid RequestId,
    Guid SubjectUserId,
    Guid CallerUserId,
    Guid? TenantId,
    string Regulation)
{
    /// <summary>
    /// Returns <see langword="true"/> when caller and subject are the same identity —
    /// the framework's default self-service mode. Call sites that construct a context
    /// where <see cref="SubjectUserId"/> differs from <see cref="CallerUserId"/> MUST
    /// have validated the <c>Privacy.Exports.ExecuteOnBehalfOf</c> permission first;
    /// the <c>GRSEC005</c> analyzer flags any construction site that passes mismatched
    /// ids without an explicit bypass.
    /// </summary>
    public bool IsSelfService => SubjectUserId == CallerUserId;
}
