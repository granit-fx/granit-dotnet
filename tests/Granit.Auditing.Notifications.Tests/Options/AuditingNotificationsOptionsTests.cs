using Granit.Auditing.Domain;
using Granit.Auditing.Notifications.Options;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Notifications.Tests.Options;

public sealed class AuditingNotificationsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        AuditingNotificationsOptions.SectionName.ShouldBe("Auditing:Notifications");

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditingNotificationsOptions options = new();

        options.AlertableCategories.ShouldBe(
            [AuditCategory.AccessDenied, AuditCategory.ConfigurationChange]);
    }
}
