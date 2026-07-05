using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Attributes;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotentAttributeTests
{
    // =========================================================================
    // Default values
    // =========================================================================

    [Fact]
    public void Constructor_DefaultRequired_IsTrue()
    {
        IdempotentAttribute attribute = new();

        attribute.Required.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_DefaultCompletedTtlSeconds_IsMinusOne()
    {
        IdempotentAttribute attribute = new();

        attribute.CompletedTtlSeconds.ShouldBe(-1);
    }

    // =========================================================================
    // Custom values
    // =========================================================================

    [Fact]
    public void Required_SetToFalse_RetainsFalse()
    {
        IdempotentAttribute attribute = new() { Required = false };

        attribute.Required.ShouldBeFalse();
    }

    [Fact]
    public void CompletedTtlSeconds_SetToCustomValue_RetainsValue()
    {
        IdempotentAttribute attribute = new() { CompletedTtlSeconds = 3600 };

        attribute.CompletedTtlSeconds.ShouldBe(3600);
    }

    // =========================================================================
    // IIdempotencyMetadata interface
    // =========================================================================

    [Fact]
    public void Attribute_ImplementsIIdempotencyMetadata()
    {
        IdempotentAttribute attribute = new();

        attribute.ShouldBeAssignableTo<IIdempotencyMetadata>();
    }

    [Fact]
    public void IIdempotencyMetadata_Required_MatchesAttributeProperty()
    {
        IdempotentAttribute attribute = new() { Required = false, CompletedTtlSeconds = 7200 };

        attribute.Required.ShouldBeFalse();
    }

    [Fact]
    public void IIdempotencyMetadata_CompletedTtlSeconds_MatchesAttributeProperty()
    {
        IdempotentAttribute attribute = new() { Required = false, CompletedTtlSeconds = 7200 };

        attribute.CompletedTtlSeconds.ShouldBe(7200);
    }

    // =========================================================================
    // AttributeUsage
    // =========================================================================

    [Fact]
    public void AttributeUsage_AllowsClassAndMethod()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(IdempotentAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Class | AttributeTargets.Method);
    }

    [Fact]
    public void AttributeUsage_DoesNotAllowMultiple()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(IdempotentAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void AttributeUsage_IsInherited()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(IdempotentAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage.Inherited.ShouldBeTrue();
    }
}
