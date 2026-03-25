using Granit.Auditing.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Dtos;

public sealed class AuditEntryDetailResponseTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        var id = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        List<AuditEntityChangeResponse> changes =
        [
            new AuditEntityChangeResponse(
                "Patient",
                "42",
                "Modified",
                [new AuditPropertyChangeResponse("Name", "Old", "New")]),
        ];

        AuditEntryDetailResponse response = new(
            id,
            timestamp,
            "user-1",
            "Jane Doe",
            "ConfigurationChange",
            "10.0.0.1",
            null,
            "trace-1",
            changes);

        response.Id.ShouldBe(id);
        response.Timestamp.ShouldBe(timestamp);
        response.UserId.ShouldBe("user-1");
        response.UserName.ShouldBe("Jane Doe");
        response.Category.ShouldBe("ConfigurationChange");
        response.EntityChanges.ShouldHaveSingleItem();
        response.EntityChanges[0].EntityType.ShouldBe("Patient");
        response.EntityChanges[0].PropertyChanges.ShouldHaveSingleItem();
    }

    [Fact]
    public void WithEmptyEntityChanges_IsValid()
    {
        AuditEntryDetailResponse response = new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "user-1",
            null,
            "DataMutation",
            null,
            null,
            null,
            []);

        response.EntityChanges.ShouldBeEmpty();
    }
}
