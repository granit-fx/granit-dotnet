using Granit.Auditing.Domain;
using Granit.Auditing.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Dtos;

public sealed class AuditingQueryParametersTests
{
    [Fact]
    public void DefaultValues_AreAllNull()
    {
        AuditingQueryParameters parameters = new();

        parameters.Page.ShouldBeNull();
        parameters.PageSize.ShouldBeNull();
        parameters.UserId.ShouldBeNull();
        parameters.EntityType.ShouldBeNull();
        parameters.EntityId.ShouldBeNull();
        parameters.Category.ShouldBeNull();
        parameters.From.ShouldBeNull();
        parameters.To.ShouldBeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset to = DateTimeOffset.UtcNow;

        AuditingQueryParameters parameters = new()
        {
            Page = 3,
            PageSize = 50,
            UserId = "user-42",
            EntityType = "Patient",
            EntityId = "123",
            Category = AuditCategory.DataAccess,
            From = from,
            To = to,
        };

        parameters.Page.ShouldBe(3);
        parameters.PageSize.ShouldBe(50);
        parameters.UserId.ShouldBe("user-42");
        parameters.EntityType.ShouldBe("Patient");
        parameters.EntityId.ShouldBe("123");
        parameters.Category.ShouldBe(AuditCategory.DataAccess);
        parameters.From.ShouldBe(from);
        parameters.To.ShouldBe(to);
    }
}
