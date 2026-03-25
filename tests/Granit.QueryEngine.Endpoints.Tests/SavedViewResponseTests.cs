// =============================================================================
// Tests - SavedViewResponse
// =============================================================================
// Vérifie que le record Response DTO expose les propriétés attendues.
// =============================================================================

using Granit.QueryEngine.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Endpoints.Tests;

public sealed class SavedViewResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        SavedViewResponse response = new(
            id, "Acme.Patients", "Active patients", "user-1",
            true, false, "{}", "[]", null, "[\"Name\",\"Email\"]");

        // Assert
        response.Id.ShouldBe(id);
        response.EntityType.ShouldBe("Acme.Patients");
        response.Name.ShouldBe("Active patients");
        response.UserId.ShouldBe("user-1");
        response.IsShared.ShouldBeTrue();
        response.IsDefault.ShouldBeFalse();
        response.FilterJson.ShouldBe("{}");
        response.SortJson.ShouldBe("[]");
        response.GroupByJson.ShouldBeNull();
        response.VisibleColumnsJson.ShouldBe("[\"Name\",\"Email\"]");
    }

    [Fact]
    public void Record_Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        new SavedViewResponse(id, "E", "N", "U", false, false, null, null, null, null)
            .ShouldBe(new SavedViewResponse(id, "E", "N", "U", false, false, null, null, null, null));
    }
}
