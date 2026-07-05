// =============================================================================
// Tests - CacheEncryptionResolver
// =============================================================================
// Vérifie la résolution du chiffrement AES par type :
//   - [CacheEncrypted] → toujours chiffrer (surcharge le flag global)
//   - [CacheEncrypted(false)] → jamais chiffrer (surcharge le flag global)
//   - Pas d'attribut → suit CachingOptions.EncryptValues
// =============================================================================

using Granit.Caching.Internal;
using Granit.Caching.Options;
using Shouldly;
using Xunit;

namespace Granit.Caching.Tests;

public sealed class CacheEncryptionResolutionTests
{
    [Fact]
    public void ShouldEncrypt_AttributeTrue_ReturnsTrueRegardlessOfGlobalFlag()
    {
        // Arrange — [CacheEncrypted] surcharge même si EncryptValues = false
        CachingOptions options = new() { EncryptValues = false };

        // Act
        bool result = CacheEncryptionResolver.ShouldEncrypt(typeof(AlwaysEncryptedItem), options);

        // Assert
        result.ShouldBeTrue("l'attribut [CacheEncrypted] doit forcer le chiffrement");
    }

    [Fact]
    public void ShouldEncrypt_AttributeFalse_ReturnsFalseRegardlessOfGlobalFlag()
    {
        // Arrange — [CacheEncrypted(false)] surcharge même si EncryptValues = true
        CachingOptions options = new() { EncryptValues = true };

        // Act
        bool result = CacheEncryptionResolver.ShouldEncrypt(typeof(NeverEncryptedItem), options);

        // Assert
        result.ShouldBeFalse("l'attribut [CacheEncrypted(false)] doit désactiver le chiffrement");
    }

    [Fact]
    public void ShouldEncrypt_NoAttribute_FollowsGlobalFlagTrue()
    {
        // Arrange
        CachingOptions options = new() { EncryptValues = true };

        // Act
        bool result = CacheEncryptionResolver.ShouldEncrypt(typeof(UnattributedItem), options);

        // Assert
        result.ShouldBeTrue("sans attribut, le flag global EncryptValues=true doit s'appliquer");
    }

    [Fact]
    public void ShouldEncrypt_NoAttribute_FollowsGlobalFlagFalse()
    {
        // Arrange
        CachingOptions options = new() { EncryptValues = false };

        // Act
        bool result = CacheEncryptionResolver.ShouldEncrypt(typeof(UnattributedItem), options);

        // Assert
        result.ShouldBeFalse("sans attribut, le flag global EncryptValues=false doit s'appliquer");
    }

    // Types de test
    [CacheEncrypted]
    private sealed class AlwaysEncryptedItem;

    [CacheEncrypted(false)]
    private sealed class NeverEncryptedItem;

    private sealed class UnattributedItem;
}
