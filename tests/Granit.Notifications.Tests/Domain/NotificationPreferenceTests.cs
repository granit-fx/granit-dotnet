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
    public void DefaultIsEnabled_IsTrue() =>
        new NotificationPreference().IsEnabled.ShouldBeTrue();
}
