using Granit.Identity.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Dtos;

public sealed class IdentityDtoTests
{
    // ──── IdentityWebhookPayload ────

    [Fact]
    public void IdentityWebhookPayload_TimestampDefaultsToNull()
    {
        var payload = new IdentityWebhookPayload("user_deleted", "user-1");

        payload.Timestamp.ShouldBeNull();
    }

    // ──── IdentityUserCacheBatchRequest ────

    [Fact]
    public void IdentityUserCacheBatchRequest_SetsUserIds()
    {
        List<string> ids = ["u1", "u2", "u3"];
        var request = new IdentityUserCacheBatchRequest(ids);

        request.UserIds.ShouldBe(ids);
        request.UserIds.Count.ShouldBe(3);
    }

    // ──── IdentityUserCacheSyncRequest ────

    [Fact]
    public void IdentityUserCacheSyncRequest_SetsUserIds()
    {
        List<string> ids = ["u1"];
        var request = new IdentityUserCacheSyncRequest(ids);

        request.UserIds.ShouldBe(ids);
    }

    // ──── IdentityUserCacheStatsResponse ────

    [Fact]
    public void IdentityUserCacheStatsResponse_SetsAllProperties()
    {
        DateTimeOffset oldest = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset newest = DateTimeOffset.UtcNow;

        var response = new IdentityUserCacheStatsResponse(100, 5, oldest, newest);

        response.TotalEntries.ShouldBe(100);
        response.StaleEntries.ShouldBe(5);
        response.OldestSyncAt.ShouldBe(oldest);
        response.NewestSyncAt.ShouldBe(newest);
    }

    [Fact]
    public void IdentityUserCacheStatsResponse_AllowsNullTimestamps()
    {
        var response = new IdentityUserCacheStatsResponse(0, 0, null, null);

        response.OldestSyncAt.ShouldBeNull();
        response.NewestSyncAt.ShouldBeNull();
    }

    // ──── IdentityProviderCapabilitiesResponse ────

    [Fact]
    public void IdentityProviderCapabilitiesResponse_SetsAllProperties()
    {
        var response = new IdentityProviderCapabilitiesResponse(
            ProviderName: "Keycloak",
            SupportsIndividualSessionTermination: true,
            SupportsNativePasswordResetEmail: true,
            SupportsGroupHierarchy: true,
            SupportsCustomAttributes: true,
            MaxCustomAttributes: 100,
            SupportsCredentialVerification: true,
            SupportsUserCreation: true,
            SupportsGroupManagement: false);

        response.ProviderName.ShouldBe("Keycloak");
        response.SupportsIndividualSessionTermination.ShouldBeTrue();
        response.SupportsNativePasswordResetEmail.ShouldBeTrue();
        response.SupportsGroupHierarchy.ShouldBeTrue();
        response.SupportsCustomAttributes.ShouldBeTrue();
        response.MaxCustomAttributes.ShouldBe(100);
        response.SupportsCredentialVerification.ShouldBeTrue();
        response.SupportsUserCreation.ShouldBeTrue();
        response.SupportsGroupManagement.ShouldBeFalse();
    }

    // ──── IdentityPasswordChangedAtResponse ────

    [Fact]
    public void IdentityPasswordChangedAtResponse_SetsChangedAt()
    {
        DateTimeOffset changedAt = DateTimeOffset.UtcNow;
        var response = new IdentityPasswordChangedAtResponse(changedAt);

        response.ChangedAt.ShouldBe(changedAt);
    }

    [Fact]
    public void IdentityPasswordChangedAtResponse_AllowsNullChangedAt()
    {
        var response = new IdentityPasswordChangedAtResponse(null);

        response.ChangedAt.ShouldBeNull();
    }

    // ──── IdentitySetTemporaryPasswordRequest ────

    [Fact]
    public void IdentitySetTemporaryPasswordRequest_SetsPassword()
    {
        var request = new IdentitySetTemporaryPasswordRequest("Temp123!");

        request.Password.ShouldBe("Temp123!");
    }

    // ──── IdentityUserCreateRequest ────

    [Fact]
    public void IdentityUserCreateRequest_SetsAllProperties()
    {
        var request = new IdentityUserCreateRequest(
            Username: "alice",
            Email: "alice@test.com",
            FirstName: "Alice",
            LastName: "Doe",
            Enabled: true,
            TemporaryPassword: "Temp123!");

        request.Username.ShouldBe("alice");
        request.Email.ShouldBe("alice@test.com");
        request.FirstName.ShouldBe("Alice");
        request.LastName.ShouldBe("Doe");
        request.Enabled.ShouldBeTrue();
        request.TemporaryPassword.ShouldBe("Temp123!");
    }

    [Fact]
    public void IdentityUserCreateRequest_AllowsNullOptionalFields()
    {
        var request = new IdentityUserCreateRequest("bob", "bob@test.com", null, null, true, null);

        request.FirstName.ShouldBeNull();
        request.LastName.ShouldBeNull();
        request.TemporaryPassword.ShouldBeNull();
    }

    // ──── IdentityUserUpdateRequest ────

    [Fact]
    public void IdentityUserUpdateRequest_SetsAllProperties()
    {
        Dictionary<string, string?> attrs = new() { ["key"] = "val" };
        var request = new IdentityUserUpdateRequest("new@test.com", "New", "Name", attrs);

        request.Email.ShouldBe("new@test.com");
        request.FirstName.ShouldBe("New");
        request.LastName.ShouldBe("Name");
        request.Attributes.ShouldNotBeNull();
    }

    [Fact]
    public void IdentityUserUpdateRequest_AllFieldsNullable()
    {
        var request = new IdentityUserUpdateRequest(null, null, null, null);

        request.Email.ShouldBeNull();
        request.FirstName.ShouldBeNull();
        request.LastName.ShouldBeNull();
        request.Attributes.ShouldBeNull();
    }

    // ──── IdentityUserSetEnabledRequest ────

    [Fact]
    public void IdentityUserSetEnabledRequest_True()
    {
        var request = new IdentityUserSetEnabledRequest(true);

        request.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void IdentityUserSetEnabledRequest_False()
    {
        var request = new IdentityUserSetEnabledRequest(false);

        request.Enabled.ShouldBeFalse();
    }
}
