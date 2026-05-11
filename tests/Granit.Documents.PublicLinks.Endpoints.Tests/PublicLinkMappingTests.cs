using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.Endpoints.Dtos;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.Endpoints.Tests;

public sealed class PublicLinkMappingTests
{
    [Fact]
    public void ToResponse_should_copy_every_safe_field_and_omit_token()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 11, 12, 0, 0, TimeSpan.Zero));
        var link = DocumentPublicLink.Create(
            id: Guid.NewGuid(),
            documentId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            tokenHash: [1, 2, 3, 4],
            scope: PublicLinkScope.View,
            expiresAt: clock.GetUtcNow().AddDays(7),
            maxUses: 5,
            timeProvider: clock);

        PublicLinkResponse response = link.ToResponse();

        response.Id.ShouldBe(link.Id);
        response.DocumentId.ShouldBe(link.DocumentId);
        response.Scope.ShouldBe(PublicLinkScope.View);
        response.ExpiresAt.ShouldBe(link.ExpiresAt);
        response.MaxUses.ShouldBe(5);
        response.CurrentUses.ShouldBe(0);
        response.RevokedAt.ShouldBeNull();
        response.RevocationReason.ShouldBeNull();
        response.CreatedAt.ShouldBe(link.CreatedAt);
        // No surface for the raw token or its HMAC digest.
        typeof(PublicLinkResponse).GetProperty("Token").ShouldBeNull();
        typeof(PublicLinkResponse).GetProperty("TokenHash").ShouldBeNull();
    }

    [Fact]
    public void ToResponse_should_surface_revocation_metadata()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 11, 12, 0, 0, TimeSpan.Zero));
        var link = DocumentPublicLink.Create(
            id: Guid.NewGuid(),
            documentId: Guid.NewGuid(),
            tenantId: null,
            tokenHash: [9, 9, 9],
            scope: PublicLinkScope.Download,
            expiresAt: clock.GetUtcNow().AddDays(30),
            maxUses: null,
            timeProvider: clock);

        clock.Advance(TimeSpan.FromHours(1));
        link.Revoke(revokedBy: Guid.NewGuid(), reason: "spam", clock);

        PublicLinkResponse response = link.ToResponse();
        response.RevokedAt.ShouldNotBeNull();
        response.RevocationReason.ShouldBe("spam");
    }
}
