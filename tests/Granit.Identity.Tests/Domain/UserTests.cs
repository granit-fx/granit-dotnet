using Granit.Identity.Domain;
using Granit.Identity.Events;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Domain;

/// <summary>
/// Locks the contract of the canonical <see cref="User"/> aggregate per
/// ADR-051. Focuses on the aggregate behaviour that downstream code
/// relies on — the bridge subscriber from B-step 5 fails closed if
/// these events stop firing.
/// </summary>
public sealed class UserTests
{
    [Fact]
    public void Create_RaisesUserCreatedEto_WithCanonicalId()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var user = User.Create(
            id: id,
            email: "alice@example.com",
            displayName: "Alice",
            firstName: "Alice",
            lastName: "Doe",
            phoneNumber: "+32 470 12 34 56",
            tenantId: tenantId);

        UserCreatedEto evt = user.IntegrationEvents
            .OfType<UserCreatedEto>()
            .ShouldHaveSingleItem();

        evt.UserId.ShouldBe(id);
        evt.DisplayName.ShouldBe("Alice");
        evt.Email.ShouldBe("alice@example.com");
        evt.FirstName.ShouldBe("Alice");
        evt.LastName.ShouldBe("Doe");
        evt.PhoneNumber.ShouldBe("+32 470 12 34 56");
        evt.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Create_HostScopedUser_RaisesEvent_WithNullTenantId()
    {
        var user = User.Create(
            id: Guid.NewGuid(),
            email: "platform@example.com",
            displayName: "Platform Admin",
            tenantId: null);

        UserCreatedEto evt = user.IntegrationEvents.OfType<UserCreatedEto>().ShouldHaveSingleItem();
        evt.TenantId.ShouldBeNull();
    }

    [Fact]
    public void UpdateProfile_RaisesUserProfileChangedEto()
    {
        // The Created event lives in the integration outbox of the
        // freshly-built aggregate; clear it so the test isolates the
        // signal of the UpdateProfile call.
        var user = User.Create(
            id: Guid.NewGuid(),
            email: "old@example.com",
            displayName: "Old Name");
        user.ClearIntegrationEvents();

        user.UpdateProfile(
            displayName: "New Name",
            email: "new@example.com",
            firstName: "New",
            lastName: "Name",
            phoneNumber: "+1 555 0100",
            preferredLocale: "en-GB",
            timezone: "Europe/London");

        UserProfileChangedEto evt = user.IntegrationEvents
            .OfType<UserProfileChangedEto>()
            .ShouldHaveSingleItem();

        evt.UserId.ShouldBe(user.Id);
        evt.DisplayName.ShouldBe("New Name");
        evt.Email.ShouldBe("new@example.com");
        evt.FirstName.ShouldBe("New");
        evt.LastName.ShouldBe("Name");
        evt.PhoneNumber.ShouldBe("+1 555 0100");
        evt.PreferredLocale.ShouldBe("en-GB");
        evt.Timezone.ShouldBe("Europe/London");
    }

    [Fact]
    public void Enable_Disable_TogglesIsEnabled_WithoutEmittingProfileEvent()
    {
        // Enable / Disable do not represent a profile change, so they
        // do not raise UserProfileChangedEto. (A future story may add
        // a dedicated UserEnabledChangedEto if a subscriber needs the
        // signal — out of scope for B-step 5.)
        var user = User.Create(
            id: Guid.NewGuid(),
            email: "user@example.com",
            displayName: "User");
        user.ClearIntegrationEvents();

        user.Disable();
        user.Enable();

        user.IntegrationEvents.OfType<UserProfileChangedEto>().ShouldBeEmpty();
    }
}
