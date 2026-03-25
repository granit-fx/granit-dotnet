using Granit.Auditing.Messages;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Messages;

public sealed class AuditPropertyChangeSnapshotTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        AuditPropertyChangeSnapshot snapshot = new("Email", "old@test.com", "new@test.com");

        snapshot.PropertyName.ShouldBe("Email");
        snapshot.OriginalValue.ShouldBe("old@test.com");
        snapshot.NewValue.ShouldBe("new@test.com");
    }

    [Fact]
    public void WithNullValues_IsValid()
    {
        AuditPropertyChangeSnapshot snapshot = new("Name", null, "New Name");

        snapshot.OriginalValue.ShouldBeNull();
        snapshot.NewValue.ShouldBe("New Name");
    }

    [Fact]
    public void SensitiveMask_CanBeUsed()
    {
        AuditPropertyChangeSnapshot snapshot = new("Password", "***", "***");

        snapshot.OriginalValue.ShouldBe("***");
        snapshot.NewValue.ShouldBe("***");
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        AuditPropertyChangeSnapshot s1 = new("Name", "A", "B");
        AuditPropertyChangeSnapshot s2 = new("Name", "A", "B");

        s1.ShouldBe(s2);
    }

    [Fact]
    public void Equality_WithDifferentValues_AreNotEqual()
    {
        AuditPropertyChangeSnapshot s1 = new("Name", "A", "B");
        AuditPropertyChangeSnapshot s2 = new("Name", "A", "C");

        s1.ShouldNotBe(s2);
    }
}
