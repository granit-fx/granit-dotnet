using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Abstractions;

public sealed class AuditLogQueryTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditLogQuery query = new();

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

        AuditLogQuery query = new(
            Page: 3,
            PageSize: 50,
            UserId: "user-42",
            EntityType: "Patient",
            EntityId: "123",
            Category: AuditLogCategory.ConfigurationChange,
            From: from,
            To: to);

        query.Page.ShouldBe(3);
        query.PageSize.ShouldBe(50);
        query.UserId.ShouldBe("user-42");
        query.EntityType.ShouldBe("Patient");
        query.EntityId.ShouldBe("123");
        query.Category.ShouldBe(AuditLogCategory.ConfigurationChange);
        query.From.ShouldBe(from);
        query.To.ShouldBe(to);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        AuditLogQuery query1 = new(Page: 2, UserId: "user-1");
        AuditLogQuery query2 = new(Page: 2, UserId: "user-1");

        query1.ShouldBe(query2);
    }

    [Fact]
    public void WithExpression_CreatesMutatedCopy()
    {
        AuditLogQuery original = new(Page: 1, UserId: "user-1");
        AuditLogQuery modified = original with { Page = 5 };

        modified.Page.ShouldBe(5);
        modified.UserId.ShouldBe("user-1");
        original.Page.ShouldBe(1);
    }
}
