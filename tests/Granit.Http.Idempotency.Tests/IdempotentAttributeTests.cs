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
    // IIdempotencyMetadata interface
    // =========================================================================

    [Fact]
    public void Attribute_ImplementsIIdempotencyMetadata()
    {
        IdempotentAttribute attribute = new();

        attribute.ShouldBeAssignableTo<IIdempotencyMetadata>();
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
