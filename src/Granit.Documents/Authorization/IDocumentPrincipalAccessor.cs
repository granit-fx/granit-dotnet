namespace Granit.Documents.Authorization;

/// <summary>
/// Resolves the <see cref="DocumentPrincipal"/> for the current request — the User id plus
/// any Role / Group memberships the share resolver should test grants against (F6.5).
/// </summary>
/// <remarks>
/// <para>
/// The default implementation reads the <c>sub</c> claim for the User id and leaves
/// <see cref="DocumentPrincipal.RoleIds"/> / <see cref="DocumentPrincipal.GroupIds"/> empty.
/// Hosted apps that ship richer membership information on the principal (e.g., role / group
/// claims sourced from their identity provider) replace this service to flow that data
/// through to the resolver.
/// </para>
/// <para>
/// Returns <c>null</c> when no authenticated principal is available — endpoints fall back
/// to <see cref="EffectivePermissionLevel.None"/> in that case so the caller's UI hides the
/// affordance rather than incorrectly enabling it.
/// </para>
/// </remarks>
public interface IDocumentPrincipalAccessor
{
    /// <summary>Returns the current request's principal, or <c>null</c> when unauthenticated.</summary>
    DocumentPrincipal? GetCurrent();
}
