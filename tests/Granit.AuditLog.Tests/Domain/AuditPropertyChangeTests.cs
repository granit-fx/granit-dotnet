using Granit.AuditLog.Domain;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Domain;

public sealed class AuditPropertyChangeTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entityChangeId = Guid.NewGuid();

        // Act
        AuditPropertyChange change = new()
        {
            Id = id,
            AuditEntityChangeId = entityChangeId,
            PropertyName = "Email",
            OriginalValue = "old@example.com",
            NewValue = "new@example.com",
        };

        // Assert
        change.Id.ShouldBe(id);
        change.AuditEntityChangeId.ShouldBe(entityChangeId);
        change.PropertyName.ShouldBe("Email");
        change.OriginalValue.ShouldBe("old@example.com");
        change.NewValue.ShouldBe("new@example.com");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditPropertyChange change = new();

        change.PropertyName.ShouldBeEmpty();
        change.OriginalValue.ShouldBeNull();
        change.NewValue.ShouldBeNull();
    }
}
