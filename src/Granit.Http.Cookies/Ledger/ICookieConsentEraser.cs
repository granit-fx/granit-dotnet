namespace Granit.Http.Cookies.Ledger;

/// <summary>
/// GDPR Art. 17 erasure primitive for the consent ledger — hard-deletes the consent
/// records attributable to a data subject.
/// </summary>
/// <remarks>
/// <para>
/// Consent records are anonymous by design (irreversibly masked IP, truncated user-agent,
/// no user identifier field): most rows are written pre-authentication by the CMP and are
/// not personal data. The single subject link is the infrastructure <c>CreatedBy</c> audit
/// field, stamped when an <em>authenticated</em> user posts a decision — those rows are the
/// ones this eraser hard-deletes. Anonymous rows (empty <c>CreatedBy</c>) are retained:
/// erasure does not apply to data that identifies nobody.
/// </para>
/// <para>
/// Wire this into the host's personal-data deletion pipeline (see
/// <c>PersonalDataDeletionRequestedEto</c> in <c>Granit.Privacy</c>). The framework ships
/// no Wolverine glue package for it: unlike Auditing, Cookies does not register a privacy
/// data provider, so an unconditional saga acknowledgement would be unexpected — hosts
/// that opt into the Privacy saga call this eraser from their own deletion handler.
/// </para>
/// <para>
/// Like every per-module personal-data eraser, invoke it within the subject's tenant
/// scope — the multi-tenant query filter applies to the delete.
/// </para>
/// </remarks>
public interface ICookieConsentEraser
{
    /// <summary>
    /// Hard-deletes all consent records created by the given user in the given tenant.
    /// </summary>
    /// <param name="userId">Subject user identifier (matched against <c>CreatedBy</c>).</param>
    /// <param name="tenantId">Tenant partition (null for host-level records).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of physically deleted records (deletion audit evidence).</returns>
    Task<int> EraseUserDataAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default);
}
