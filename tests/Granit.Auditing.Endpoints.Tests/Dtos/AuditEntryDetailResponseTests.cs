using Granit.Auditing.Domain;
using Granit.Auditing.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Dtos;

public sealed class AuditEntryDetailResponseTests
{
    [Fact]
    public void WithEmptyEntityChanges_IsValid()
    {
        AuditEntryDetailResponse response = new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "user-1",
            null,
            AuditCategory.DataMutation,
            null,
            null,
            null,
            null,
            []);

        response.EntityChanges.ShouldBeEmpty();
    }
}
