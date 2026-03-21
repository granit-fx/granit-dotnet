using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="RecordTransitionRequest"/> record.
/// </summary>
public sealed class RecordTransitionRequestTests
{
    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        RecordTransitionRequest request = new()
        {
            EntityType = "Invoice",
            EntityId = "inv-123",
            PreviousState = "Draft",
            NewState = "Published",
            UserId = "user-42",
            Comment = "Approved by manager",
            TenantId = tenantId,
        };

        // Assert
        request.EntityType.ShouldBe("Invoice");
        request.EntityId.ShouldBe("inv-123");
        request.PreviousState.ShouldBe("Draft");
        request.NewState.ShouldBe("Published");
        request.UserId.ShouldBe("user-42");
        request.Comment.ShouldBe("Approved by manager");
        request.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void OptionalProperties_ShouldDefaultToNull()
    {
        // Act
        RecordTransitionRequest request = new()
        {
            EntityType = "Invoice",
            EntityId = "inv-1",
            PreviousState = "Draft",
            NewState = "Published",
            UserId = "user-1",
        };

        // Assert
        request.Comment.ShouldBeNull();
        request.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        RecordTransitionRequest req1 = new()
        {
            EntityType = "Doc",
            EntityId = "1",
            PreviousState = "A",
            NewState = "B",
            UserId = "user",
        };

        RecordTransitionRequest req2 = new()
        {
            EntityType = "Doc",
            EntityId = "1",
            PreviousState = "A",
            NewState = "B",
            UserId = "user",
        };

        // Assert
        req1.ShouldBe(req2);
    }
}
