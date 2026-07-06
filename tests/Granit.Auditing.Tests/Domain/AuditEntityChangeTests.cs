using Granit.Auditing.Domain;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Domain;

public sealed class AuditEntityChangeTests
{
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
