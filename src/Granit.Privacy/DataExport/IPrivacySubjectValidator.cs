namespace Granit.Privacy.DataExport;

/// <summary>
/// Tenant-scoped validator the privacy module calls before queuing a DSR
/// (data subject request) for a subject the caller named in the request body
/// — typically the <c>POST /privacy/exports/on-behalf-of</c> admin path.
/// </summary>
/// <remarks>
/// <para>
/// The admin DSR endpoint trusts <c>SubjectUserId</c> from the request body.
/// Without a tenant-bound existence check, an operator in tenant A can ask
/// for an export naming a subject id that belongs to tenant B; the saga then
/// runs against tenant A's providers but the audit trail records a DSR for
/// a foreign subject (ROPA pollution, existence-probe vector).
/// </para>
/// <para>
/// Implementations MUST resolve the subject inside the current tenant context
/// (typically via <c>ICurrentTenant</c> + the host's user store). Returning
/// <see langword="false"/> causes the endpoint to respond <c>404 Not Found</c>
/// — same status as a non-existent request id, so existence cannot be probed.
/// </para>
/// <para>
/// The default registration is <c>NullPrivacySubjectValidator</c> (always
/// returns <see langword="true"/>) so apps without an admin DSR surface are
/// unaffected. Hosts that expose <c>Privacy.Exports.ExecuteOnBehalfOf</c> MUST
/// register a real implementation — see
/// <c>UseUserLookupForPrivacySubjectValidation</c> in
/// <c>Granit.Privacy.Endpoints</c>.
/// </para>
/// </remarks>
public interface IPrivacySubjectValidator
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="subjectUserId"/>
    /// names a user visible inside the current tenant scope.
    /// </summary>
    /// <param name="subjectUserId">The data subject id supplied in the request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> SubjectExistsInCurrentTenantAsync(
        Guid subjectUserId,
        CancellationToken cancellationToken);
}
