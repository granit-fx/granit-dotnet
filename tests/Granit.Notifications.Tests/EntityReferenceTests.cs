// =============================================================================
// Tests - EntityReference
// =============================================================================
// Verifies the record type: construction, value equality, ToString, and
// property access for the polymorphic entity reference (Odoo-style chatter).
// =============================================================================

using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class EntityReferenceTests
{
    [Fact]
    public void Constructor_SetsEntityType()
    {
        EntityReference reference = new("Invoice", "inv-42");

        reference.EntityType.ShouldBe("Invoice");
    }

    [Fact]
    public void Constructor_SetsEntityId()
    {
        EntityReference reference = new("Invoice", "inv-42");

        reference.EntityId.ShouldBe("inv-42");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        EntityReference a = new("Patient", "pat-1");
        EntityReference b = new("Patient", "pat-1");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentEntityType_AreNotEqual()
    {
        EntityReference a = new("Patient", "pat-1");
        EntityReference b = new("Document", "pat-1");

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentEntityId_AreNotEqual()
    {
        EntityReference a = new("Patient", "pat-1");
        EntityReference b = new("Patient", "pat-2");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void GetHashCode_SameValues_ProduceSameHash()
    {
        EntityReference a = new("Invoice", "inv-42");
        EntityReference b = new("Invoice", "inv-42");

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsTypeAndId()
    {
        EntityReference reference = new("Invoice", "inv-42");

        string result = reference.ToString();

        result.ShouldContain("Invoice");
        result.ShouldContain("inv-42");
    }
}
