// =============================================================================
// Tests - SavedViewResponse
// =============================================================================
// Vérifie que le record Response DTO expose les propriétés attendues.
// =============================================================================

using Granit.QueryEngine.AspNetCore.Dtos;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests;

public sealed class SavedViewResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        SavedViewResponse response = new(
            id, "Acme.Patients", "Active patients", true,
            true, false, "{}", "[]", null, "[\"Name\",\"Email\"]");

        // Assert
        response.Id.ShouldBe(id);
        response.EntityType.ShouldBe("Acme.Patients");
        response.Name.ShouldBe("Active patients");
        response.IsOwner.ShouldBeTrue();
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
        new SavedViewResponse(id, "E", "N", true, false, false, null, null, null, null)
            .ShouldBe(new SavedViewResponse(id, "E", "N", true, false, false, null, null, null, null));
    }
}
