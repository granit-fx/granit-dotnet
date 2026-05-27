namespace Granit.Privacy.DataExport.Internal;

/// <summary>
/// Default <see cref="IPrivacySubjectValidator"/> that accepts every subject
/// id — registered by <c>AddGranitPrivacy</c> for backward compatibility with
/// hosts that don't expose the admin DSR endpoint.
/// </summary>
/// <remarks>
/// Hosts that map <c>POST /privacy/exports/on-behalf-of</c> MUST replace this
/// with a tenant-bound implementation (e.g. via
/// <c>UseUserLookupForPrivacySubjectValidation</c>) — otherwise an operator
/// can issue admin DSR requests naming subjects in other tenants.
/// </remarks>
internal sealed class NullPrivacySubjectValidator : IPrivacySubjectValidator
{
    public Task<bool> SubjectExistsInCurrentTenantAsync(
        Guid subjectUserId,
        CancellationToken cancellationToken) => Task.FromResult(true);
}
