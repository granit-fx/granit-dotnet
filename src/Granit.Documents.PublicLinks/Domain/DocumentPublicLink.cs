using Granit.Documents.PublicLinks.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Documents.PublicLinks.Domain;

/// <summary>
/// Aggregate root representing a shareable public link to a <c>Document</c>.
/// Bearer tokens are HMAC-hashed at rest: the raw token is returned exactly once
/// at creation time and never persisted in cleartext.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle: <c>Active</c> (created, not revoked, not expired, uses remaining) →
/// <c>Revoked</c> (operator action) or implicitly <c>Exhausted</c> when
/// <see cref="CurrentUses"/> reaches <see cref="MaxUses"/> or <see cref="ExpiresAt"/>
/// has passed. Status is computed, not stored — see <see cref="IsActive"/>.
/// </para>
/// <para>
/// Defensive validation is enforced on every state transition: a consumed,
/// revoked, or expired link refuses further use. Storage / endpoints / Wolverine
/// integration land in companion packages (F18.2 / .3 / .4).
/// </para>
/// </remarks>
public sealed class DocumentPublicLink : AggregateRoot, IMultiTenant
{
    /// <summary>Parameterless constructor required by the EF Core materialiser.</summary>
    private DocumentPublicLink() { }

    /// <summary>
    /// Creates a new active public link. The caller is responsible for hashing the
    /// random token with the host's HMAC pepper — see <c>PublicLinkTokenFactory</c>.
    /// </summary>
    public static DocumentPublicLink Create(
        Guid id,
        Guid documentId,
        Guid? tenantId,
        byte[] tokenHash,
        PublicLinkScope scope,
        DateTimeOffset expiresAt,
        int? maxUses,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (tokenHash.Length == 0)
        {
            throw new ArgumentException("Token hash must not be empty.", nameof(tokenHash));
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (expiresAt <= now)
        {
            throw new ArgumentException(
                $"ExpiresAt ({expiresAt:O}) must be strictly in the future (now: {now:O}).",
                nameof(expiresAt));
        }
        if (maxUses is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxUses),
                maxUses,
                "MaxUses must be null (unlimited) or a positive integer.");
        }

        var link = new DocumentPublicLink
        {
            Id = id,
            TenantId = tenantId,
            DocumentId = documentId,
            TokenHash = tokenHash,
            Scope = scope,
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
            CurrentUses = 0,
            CreatedAt = now,
        };

        link.AddDomainEvent(new DocumentPublicLinkCreatedEvent(
            link.Id, tenantId, documentId, scope, expiresAt, maxUses, now));
        return link;
    }

    /// <inheritdoc cref="IMultiTenant.TenantId" />
    public Guid? TenantId { get; private set; }

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Identifier of the document the link points at.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>HMAC-SHA256 digest of the bearer token. Never the raw token.</summary>
    public byte[] TokenHash { get; private set; } = [];

    /// <summary>Granted scope for the bearer.</summary>
    public PublicLinkScope Scope { get; private set; }

    /// <summary>UTC instant after which the link is no longer accepted.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Optional cap on the number of successful redemptions. <c>null</c> means unlimited.</summary>
    public int? MaxUses { get; private set; }

    /// <summary>Number of times the link has been redeemed.</summary>
    public int CurrentUses { get; private set; }

    /// <summary>UTC instant the link was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>UTC instant the link was revoked, or <c>null</c> if still live.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Identifier of the user / system principal that revoked the link.</summary>
    public Guid? RevokedBy { get; private set; }

    /// <summary>Operator-supplied free-text reason for the revocation.</summary>
    public string? RevocationReason { get; private set; }

    /// <summary>Whether the link is still redeemable at <paramref name="now"/>.</summary>
    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && now < ExpiresAt && (MaxUses is null || CurrentUses < MaxUses);

    /// <summary>Revokes the link. Idempotent guard: throws if already revoked.</summary>
    public void Revoke(Guid? revokedBy, string? reason, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException(
                $"DocumentPublicLink {Id} is already revoked (since {RevokedAt:O}).");
        }
        DateTimeOffset now = timeProvider.GetUtcNow();
        RevokedAt = now;
        RevokedBy = revokedBy;
        RevocationReason = reason;
        AddDomainEvent(new DocumentPublicLinkRevokedEvent(
            Id, TenantId, DocumentId, revokedBy, reason, now));
    }

    /// <summary>Records a single successful redemption. Throws if the link is no longer redeemable.</summary>
    public void RegisterConsumption(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException(
                $"DocumentPublicLink {Id} was revoked at {RevokedAt:O}.");
        }
        if (now >= ExpiresAt)
        {
            throw new InvalidOperationException(
                $"DocumentPublicLink {Id} expired at {ExpiresAt:O}.");
        }
        if (MaxUses is not null && CurrentUses >= MaxUses)
        {
            throw new InvalidOperationException(
                $"DocumentPublicLink {Id} reached its MaxUses cap ({MaxUses}).");
        }
        CurrentUses++;
        AddDomainEvent(new DocumentPublicLinkConsumedEvent(
            Id, TenantId, DocumentId, Scope, CurrentUses, now));
    }
}
