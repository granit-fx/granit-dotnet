// =============================================================================
// Tests - DisableDateTimeNormalizationAttribute
// =============================================================================
// Verifies that the attribute:
//   - Can be applied to classes, properties, and parameters
//   - Has the correct AttributeUsage configuration
//   - Inherits from System.Attribute
// =============================================================================

using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Timing.Tests;

public sealed class DisableDateTimeNormalizationAttributeTests
{
    [Fact]
    public void Attribute_InheritsFromSystemAttribute()
    {
        DisableDateTimeNormalizationAttribute attribute = new();

        attribute.ShouldBeAssignableTo<Attribute>();
    }

    [Fact]
    public void AttributeUsage_AllowsClass()
    {
        AttributeUsageAttribute? usage = typeof(DisableDateTimeNormalizationAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Class).ShouldBe(AttributeTargets.Class);
    }

    [Fact]
    public void AttributeUsage_AllowsProperty()
    {
        AttributeUsageAttribute? usage = typeof(DisableDateTimeNormalizationAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Property).ShouldBe(AttributeTargets.Property);
    }

    [Fact]
    public void AttributeUsage_AllowsParameter()
    {
        AttributeUsageAttribute? usage = typeof(DisableDateTimeNormalizationAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Parameter).ShouldBe(AttributeTargets.Parameter);
    }

    [Fact]
    public void AttributeUsage_DoesNotAllowMethod()
    {
        AttributeUsageAttribute? usage = typeof(DisableDateTimeNormalizationAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Method).ShouldBe((AttributeTargets)0);
    }

    [Fact]
    public void Attribute_CanBeAppliedToClass()
    {
        DisableDateTimeNormalizationAttribute? attribute = typeof(AnnotatedClass)
            .GetCustomAttribute<DisableDateTimeNormalizationAttribute>();

        attribute.ShouldNotBeNull();
    }

    [Fact]
    public void Attribute_CanBeAppliedToProperty()
    {
        PropertyInfo? property = typeof(AnnotatedProperty).GetProperty(nameof(AnnotatedProperty.MyDate));

        DisableDateTimeNormalizationAttribute? attribute = property?
            .GetCustomAttribute<DisableDateTimeNormalizationAttribute>();

        attribute.ShouldNotBeNull();
    }

    [Fact]
    public void Attribute_CanBeAppliedToParameter()
    {
        MethodInfo? method = typeof(AnnotatedParameter)
            .GetMethod(nameof(AnnotatedParameter.Process));
        ParameterInfo? parameter = method?.GetParameters().FirstOrDefault();

        DisableDateTimeNormalizationAttribute? attribute = parameter?
            .GetCustomAttribute<DisableDateTimeNormalizationAttribute>();

        attribute.ShouldNotBeNull();
    }

    // Test fixtures for reflection-based tests

    [DisableDateTimeNormalization]
    private sealed class AnnotatedClass;

    private sealed class AnnotatedProperty
    {
        [DisableDateTimeNormalization]
        public DateTimeOffset MyDate { get; set; }
    }

    private sealed class AnnotatedParameter
    {
        public static void Process([DisableDateTimeNormalization] DateTimeOffset date) { }
    }
}
