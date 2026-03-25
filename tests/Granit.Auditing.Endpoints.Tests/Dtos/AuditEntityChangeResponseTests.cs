using Granit.Auditing.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Dtos;

public sealed class AuditEntityChangeResponseTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        List<AuditPropertyChangeResponse> propertyChanges =
        [
            new AuditPropertyChangeResponse("Email", "old@test.com", "new@test.com"),
            new AuditPropertyChangeResponse("Name", "Old Name", "New Name"),
        ];

        AuditEntityChangeResponse response = new(
            "Patient",
            "42",
            "Modified",
            propertyChanges);

        response.EntityType.ShouldBe("Patient");
        response.EntityId.ShouldBe("42");
        response.ChangeType.ShouldBe("Modified");
        response.PropertyChanges.Count.ShouldBe(2);
    }

    [Fact]
    public void WithEmptyPropertyChanges_IsValid()
    {
        AuditEntityChangeResponse response = new("Invoice", "INV-001", "Created", []);

        response.PropertyChanges.ShouldBeEmpty();
    }
}
