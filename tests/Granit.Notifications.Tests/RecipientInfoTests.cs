// =============================================================================
// Tests - RecipientInfo
// =============================================================================
// Verifies the record type for notification recipients: required UserId,
// optional contact fields, and record value equality.
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class RecipientInfoTests
{
    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        RecipientInfo info = new() { UserId = "user-1" };

        info.Email.ShouldBeNull();
        info.PhoneNumber.ShouldBeNull();
        info.PreferredCulture.ShouldBeNull();
        info.DisplayName.ShouldBeNull();
    }

    [Fact]
    public void AllProperties_CanBeSetViaInitializers()
    {
        RecipientInfo info = new()
        {
            UserId = "user-42",
            Email = "user@example.com",
            PhoneNumber = "+32470123456",
            PreferredCulture = "fr-BE",
            DisplayName = "Jean Dupont",
        };

        info.Email.ShouldBe("user@example.com");
        info.PhoneNumber.ShouldBe("+32470123456");
        info.PreferredCulture.ShouldBe("fr-BE");
        info.DisplayName.ShouldBe("Jean Dupont");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        RecipientInfo a = new()
        {
            UserId = "user-1",
            Email = "a@test.com",
            PhoneNumber = "+32470000000",
            PreferredCulture = "nl-BE",
            DisplayName = "Test User",
        };

        RecipientInfo b = new()
        {
            UserId = "user-1",
            Email = "a@test.com",
            PhoneNumber = "+32470000000",
            PreferredCulture = "nl-BE",
            DisplayName = "Test User",
        };

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentUserId_AreNotEqual()
    {
        RecipientInfo a = new() { UserId = "user-1" };
        RecipientInfo b = new() { UserId = "user-2" };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentEmail_AreNotEqual()
    {
        RecipientInfo a = new() { UserId = "user-1", Email = "a@test.com" };
        RecipientInfo b = new() { UserId = "user-1", Email = "b@test.com" };

        a.ShouldNotBe(b);
    }
}
