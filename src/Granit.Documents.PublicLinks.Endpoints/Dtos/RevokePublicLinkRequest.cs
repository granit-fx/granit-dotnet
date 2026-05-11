namespace Granit.Documents.PublicLinks.Endpoints.Dtos;

/// <summary>
/// Input shape for <c>DELETE /public-links/{id}</c>. The optional
/// <see cref="Reason"/> is recorded on the aggregate for the audit trail.
/// </summary>
public sealed record RevokePublicLinkRequest(string? Reason)
{
    /// <summary>Hard ceiling on the free-text revocation reason, in characters.</summary>
    public const int MaxReasonLength = 500;
}
