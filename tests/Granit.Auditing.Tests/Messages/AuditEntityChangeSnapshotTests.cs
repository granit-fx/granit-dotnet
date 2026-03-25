using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Messages;

public sealed class AuditEntityChangeSnapshotTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        List<AuditPropertyChangeSnapshot> propertyChanges =
        [
            new AuditPropertyChangeSnapshot("Name", "Old", "New"),
        ];

        AuditEntityChangeSnapshot snapshot = new(
            "Patient",
            "42",
            AuditChangeType.Modified,
            propertyChanges);

        snapshot.EntityType.ShouldBe("Patient");
        snapshot.EntityId.ShouldBe("42");
        snapshot.ChangeType.ShouldBe(AuditChangeType.Modified);
        snapshot.PropertyChanges.ShouldHaveSingleItem();
        snapshot.PropertyChanges[0].PropertyName.ShouldBe("Name");
    }

    [Fact]
    public void WithEmptyPropertyChanges_IsValid()
    {
        AuditEntityChangeSnapshot snapshot = new(
            "Invoice",
            "INV-001",
            AuditChangeType.Created,
            []);

        snapshot.PropertyChanges.ShouldBeEmpty();
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        AuditEntityChangeSnapshot snapshot1 = new("Patient", "1", AuditChangeType.Deleted, []);
        AuditEntityChangeSnapshot snapshot2 = new("Patient", "1", AuditChangeType.Deleted, []);

        snapshot1.ShouldBe(snapshot2);
    }
}
