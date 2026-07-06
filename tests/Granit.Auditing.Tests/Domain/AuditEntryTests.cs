using Granit.Auditing.Domain;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Domain;

public sealed class AuditEntryTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Act
        AuditEntry entry = new();

        // Assert
        entry.Id.ShouldBe(Guid.Empty);
        entry.Timestamp.ShouldBe(default);
        entry.UserId.ShouldBeEmpty();
        entry.UserName.ShouldBeNull();
        entry.Category.ShouldBe(AuditCategory.DataMutation);
        entry.IpAddress.ShouldBeNull();
        entry.UserAgent.ShouldBeNull();
        entry.TenantId.ShouldBeNull();
        entry.CorrelationId.ShouldBeNull();
        entry.EntityChanges.ShouldBeEmpty();
    }
}
