using Granit.Auditing.Domain;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Abstractions;

public sealed class AuditingQueryTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditingQuery query = new();

        query.Page.ShouldBe(1);
        query.PageSize.ShouldBe(QueryEngineDefaults.DefaultPageSize);
        query.UserId.ShouldBeNull();
        query.EntityType.ShouldBeNull();
        query.EntityId.ShouldBeNull();
        query.Category.ShouldBeNull();
        query.From.ShouldBeNull();
        query.To.ShouldBeNull();
    }

    [Fact]
    public void WithAllFilters_SetsAllProperties()
    {
        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset to = DateTimeOffset.UtcNow;

        AuditingQuery query = new(
            Page: 3,
            PageSize: 50,
            UserId: "user-42",
            EntityType: "Patient",
            EntityId: "123",
            Category: AuditCategory.ConfigurationChange,
            From: from,
            To: to);

        query.Page.ShouldBe(3);
        query.PageSize.ShouldBe(50);
        query.UserId.ShouldBe("user-42");
        query.EntityType.ShouldBe("Patient");
        query.EntityId.ShouldBe("123");
        query.Category.ShouldBe(AuditCategory.ConfigurationChange);
        query.From.ShouldBe(from);
        query.To.ShouldBe(to);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        AuditingQuery query1 = new(Page: 2, UserId: "user-1");
        AuditingQuery query2 = new(Page: 2, UserId: "user-1");

        query1.ShouldBe(query2);
    }

    [Fact]
    public void WithExpression_CreatesMutatedCopy()
    {
        AuditingQuery original = new(Page: 1, UserId: "user-1");
        AuditingQuery modified = original with { Page = 5 };

        modified.Page.ShouldBe(5);
        modified.UserId.ShouldBe("user-1");
        original.Page.ShouldBe(1);
    }
}
