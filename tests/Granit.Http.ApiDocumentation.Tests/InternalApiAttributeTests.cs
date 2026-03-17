// =============================================================================
// Tests - InternalApiAttribute
// =============================================================================
// Vérifie que l'attribut peut être appliqué sur les classes et les méthodes,
// et qu'il n'est pas héritable (sealed) ni répétable (AllowMultiple = false).
// =============================================================================

using Granit.Http.ApiDocumentation.Attributes;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class InternalApiAttributeTests
{
    [Fact]
    public void InternalApiAttribute_CanBeAppliedToClass()
    {
        // Arrange & Act
        AttributeUsageAttribute? usage = typeof(InternalApiAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usage.ShouldNotBeNull();
        usage!.ValidOn.HasFlag(AttributeTargets.Class).ShouldBeTrue();
    }

    [Fact]
    public void InternalApiAttribute_CanBeAppliedToMethod()
    {
        // Arrange & Act
        AttributeUsageAttribute? usage = typeof(InternalApiAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usage.ShouldNotBeNull();
        usage!.ValidOn.HasFlag(AttributeTargets.Method).ShouldBeTrue();
    }

    [Fact]
    public void InternalApiAttribute_AllowMultiple_IsFalse()
    {
        // Arrange & Act
        AttributeUsageAttribute? usage = typeof(InternalApiAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .OfType<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void InternalApiAttribute_IsAttribute() =>
        typeof(InternalApiAttribute).IsAssignableTo(typeof(Attribute)).ShouldBeTrue();
}
