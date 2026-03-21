using Granit.Caching.Options;
using Shouldly;
using Xunit;

namespace Granit.Caching.Tests;

public sealed class CacheEncryptionOptionsTests
{
    [Fact]
    public void SectionName_IsCacheEncryption() => CacheEncryptionOptions.SectionName.ShouldBe("Cache:Encryption");

    [Fact]
    public void Defaults_Key_IsNull()
    {
        CacheEncryptionOptions options = new();

        options.Key.ShouldBeNull();
    }

    [Fact]
    public void Key_CanBeSet()
    {
        CacheEncryptionOptions options = new() { Key = "dGVzdC1rZXk=" };

        options.Key.ShouldBe("dGVzdC1rZXk=");
    }
}
