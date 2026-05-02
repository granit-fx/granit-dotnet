namespace Granit.Authorization;

/// <summary>
/// Immutable projection of the current principal consumed by
/// <see cref="IPermissionGrantProvider"/> implementations.
/// </summary>
/// <param name="UserId">
/// Subject (<c>sub</c>) claim value, or <see langword="null"/> for
/// anonymous. Per ADR-051 B-step 4 this resolves to the canonical
/// <see cref="Granit.Identity.Domain.User.Id"/> stringified — the
/// same value is held in <c>LocalIdentity.Id</c> and
/// <c>FederatedIdentity.UserId</c> so the lookup matches grants
/// regardless of the login path.
/// </param>
/// <param name="Roles">Role claims associated with the principal (never <see langword="null"/>, possibly empty).</param>
/// <param name="ClientId">OIDC <c>client_id</c> claim value, or <see langword="null"/> when absent.</param>
public sealed record PermissionGrantLookupContext(
    string? UserId,
    IReadOnlyList<string> Roles,
    string? ClientId);
