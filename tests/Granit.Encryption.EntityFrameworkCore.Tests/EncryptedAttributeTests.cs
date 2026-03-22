using System.Reflection;
using Granit.Encryption.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Encryption.EntityFrameworkCore.Tests;

public sealed class EncryptedAttributeTests
{
    [Fact]
    public void EncryptedAttribute_TargetsPropertyOnly()
    {
        AttributeUsageAttribute? usage = typeof(EncryptedAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Property);
    }

    [Fact]
    public void EncryptedAttribute_CanBeApplied_ToStringProperty()
    {
        PropertyInfo? property = typeof(TestEntity)
            .GetProperty(nameof(TestEntity.SecretValue));

        EncryptedAttribute? attr = property?.GetCustomAttribute<EncryptedAttribute>();

        attr.ShouldNotBeNull();
    }

    [Fact]
    public void EncryptedAttribute_IsNotPresent_OnNonAnnotatedProperty()
    {
        PropertyInfo? property = typeof(TestEntity)
            .GetProperty(nameof(TestEntity.PlainValue));

        EncryptedAttribute? attr = property?.GetCustomAttribute<EncryptedAttribute>();

        attr.ShouldBeNull();
    }

    [Fact]
    public void EncryptedAttribute_KeyIsolation_DefaultsFalse()
    {
        PropertyInfo? property = typeof(TestEntity)
            .GetProperty(nameof(TestEntity.SecretValue));

        EncryptedAttribute? attr = property?.GetCustomAttribute<EncryptedAttribute>();

        attr.ShouldNotBeNull();
        attr.KeyIsolation.ShouldBeFalse();
    }

    [Fact]
    public void EncryptedAttribute_KeyIsolation_CanBeSetToTrue()
    {
        PropertyInfo? property = typeof(TestEntity)
            .GetProperty(nameof(TestEntity.IsolatedValue));

        EncryptedAttribute? attr = property?.GetCustomAttribute<EncryptedAttribute>();

        attr.ShouldNotBeNull();
        attr.KeyIsolation.ShouldBeTrue();
    }

    private sealed class TestEntity
    {
        [Encrypted]
        public string SecretValue { get; set; } = string.Empty;

        [Encrypted(KeyIsolation = true)]
        public string IsolatedValue { get; set; } = string.Empty;

        public string PlainValue { get; set; } = string.Empty;
    }
}
