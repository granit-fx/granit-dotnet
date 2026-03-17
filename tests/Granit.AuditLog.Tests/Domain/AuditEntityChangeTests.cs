using Granit.AuditLog.Domain;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Domain;

public sealed class AuditEntityChangeTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Act
        AuditEntityChange change = new()
        {
            Id = id,
            AuditLogEntryId = entryId,
            EntityType = "Patient",
            EntityId = "12345",
            ChangeType = AuditChangeType.Modified,
        };

        // Assert
        change.Id.ShouldBe(id);
        change.AuditLogEntryId.ShouldBe(entryId);
        change.EntityType.ShouldBe("Patient");
        change.EntityId.ShouldBe("12345");
        change.ChangeType.ShouldBe(AuditChangeType.Modified);
        change.PropertyChanges.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditEntityChange change = new();

        change.EntityType.ShouldBeEmpty();
        change.EntityId.ShouldBeEmpty();
        change.ChangeType.ShouldBe(AuditChangeType.Created);
        change.PropertyChanges.ShouldBeEmpty();
    }
}
