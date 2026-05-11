using Granit.Documents.PublicLinks.Domain;

namespace Granit.Documents.PublicLinks.Endpoints.Dtos;

/// <summary>
/// Input shape for <c>POST /documents/{id}/public-links</c>. The host clamps
/// <see cref="TtlDays"/> down to <c>GranitDocumentsPublicLinksOptions.MaxTtl</c>
/// and substitutes the default when <c>0</c> is supplied; the same applies to
/// <see cref="MaxUses"/> against <c>DefaultMaxUses</c>.
/// </summary>
/// <param name="Scope">Granted scope — download or inline view.</param>
/// <param name="TtlDays">
/// Validity window expressed in whole days. Must be at least <c>1</c>; values
/// exceeding the configured ceiling are clamped down server-side.
/// </param>
/// <param name="MaxUses">
/// Optional cap on the number of successful redemptions. <c>null</c> falls back
/// to <c>GranitDocumentsPublicLinksOptions.DefaultMaxUses</c> (which may itself be
/// <c>null</c> — unlimited within the TTL window).
/// </param>
public sealed record CreatePublicLinkRequest(
    PublicLinkScope Scope,
    int TtlDays,
    int? MaxUses);
