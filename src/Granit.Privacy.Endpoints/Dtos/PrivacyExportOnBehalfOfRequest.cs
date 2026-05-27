namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Body for <c>POST /privacy/exports/on-behalf-of</c> — the admin DSR
/// (Data Subject Request) path where an authenticated administrator triggers
/// an export for a different data subject.
/// </summary>
/// <remarks>
/// <para>
/// Gated by the dedicated <c>Privacy.Exports.ExecuteOnBehalfOf</c> permission — distinct
/// from the self-service <c>Privacy.Exports.Execute</c> so RBAC policies can
/// grant the admin path to a narrow operator role without unlocking it for
/// every authenticated user.
/// </para>
/// <para>
/// The audit trail records both <c>CallerUserId</c> (the administrator) and
/// <c>SubjectUserId</c> (the data subject) so the substitution is fully
/// reconstructable — this is the GDPR Art. 30 ROPA-relevant signal that the
/// access was an admin DSR, not a self-service one.
/// </para>
/// </remarks>
/// <param name="SubjectUserId">Identifier of the data subject the export targets.
/// MUST be a real account known to the host — there's no anonymous-subject path.</param>
/// <param name="Scopes">Subset of provider names to include in the archive.
/// Same semantics as the self-service endpoint: <see langword="null"/> / empty
/// means "all visible scopes".</param>
public sealed record PrivacyExportOnBehalfOfRequest(
    Guid SubjectUserId,
    IReadOnlyList<string>? Scopes = null);
