using Shouldly;
using Xunit;

namespace Granit.Caching.Tests;

public sealed class CacheEncryptedAttributeTests
{
    [Fact]
    public void Constructor_DefaultParameter_EncryptIsTrue()
    {
        CacheEncryptedAttribute attribute = new();

        attribute.Encrypt.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_ExplicitTrue_EncryptIsTrue()
    {
        CacheEncryptedAttribute attribute = new(true);

        attribute.Encrypt.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_ExplicitFalse_EncryptIsFalse()
    {
        CacheEncryptedAttribute attribute = new(false);

        attribute.Encrypt.ShouldBeFalse();
    }

    [Fact]
    public void AttributeUsage_TargetsClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(CacheEncryptedAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.ValidOn.ShouldBe(AttributeTargets.Class);
    }
}
