using Granit.Domain;
using Granit.Notifications.Domain;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests.Domain;

public sealed class NotificationPreferenceTests
{
    [Fact]
    public void InheritsAuditedEntity() =>
        typeof(NotificationPreference).IsAssignableTo(typeof(AuditedEntity)).ShouldBeTrue();

    [Fact]
    public void ImplementsIMultiTenant() =>
        typeof(NotificationPreference).IsAssignableTo(typeof(IMultiTenant)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(NotificationPreference).IsSealed.ShouldBeTrue();

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        NotificationPreference preference = new();

        preference.Id.ShouldBe(Guid.Empty);
        preference.UserId.ShouldBe(string.Empty);
        preference.NotificationTypeName.ShouldBe(string.Empty);
        preference.ChannelName.ShouldBe(string.Empty);
        preference.IsEnabled.ShouldBeTrue();
        preference.TenantId.ShouldBeNull();
        preference.CreatedAt.ShouldBe(default);
        preference.CreatedBy.ShouldBe(string.Empty);
        preference.ModifiedAt.ShouldBeNull();
        preference.ModifiedBy.ShouldBeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        NotificationPreference preference = new()
        {
            UserId = "user-42",
            NotificationTypeName = "order.created",
            ChannelName = "Email",
            IsEnabled = false,
            TenantId = tenantId,
            CreatedAt = now,
            CreatedBy = "user-42",
            ModifiedAt = now.AddHours(1),
            ModifiedBy = "user-42",
        };

        preference.UserId.ShouldBe("user-42");
        preference.NotificationTypeName.ShouldBe("order.created");
        preference.ChannelName.ShouldBe("Email");
        preference.IsEnabled.ShouldBeFalse();
        preference.TenantId.ShouldBe(tenantId);
        preference.CreatedAt.ShouldBe(now);
        preference.CreatedBy.ShouldBe("user-42");
        preference.ModifiedAt.ShouldBe(now.AddHours(1));
        preference.ModifiedBy.ShouldBe("user-42");
    }

    [Fact]
    public void DefaultIsEnabled_IsTrue() =>
        new NotificationPreference().IsEnabled.ShouldBeTrue();
}
