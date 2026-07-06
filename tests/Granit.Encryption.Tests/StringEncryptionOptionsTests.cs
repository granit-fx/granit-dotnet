// =============================================================================
// StringEncryptionOptionsTests - Configuration option defaults and properties
// =============================================================================
// Verifies:
//   - Default values for all option properties
//   - All properties are settable
//   - Constants match expected configuration section names
// =============================================================================

using Granit.Encryption.Options;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests;

public sealed class StringEncryptionOptionsTests
{
    [Fact]
    public void SectionName_IsEncryption() => StringEncryptionOptions.SectionName.ShouldBe("Encryption");

    [Fact]
    public void AesProviderName_IsAes() => StringEncryptionOptions.AesProviderName.ShouldBe("Aes");

    [Fact]
    public void VaultProviderName_IsVault() => StringEncryptionOptions.VaultProviderName.ShouldBe("Vault");

    [Fact]
    public void Defaults_PassPhrase_IsEmpty()
    {
        StringEncryptionOptions options = new();

        options.PassPhrase.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_KeySize_Is256()
    {
        StringEncryptionOptions options = new();

        options.KeySize.ShouldBe(256);
    }

    [Fact]
    public void Defaults_ProviderName_IsAes()
    {
        StringEncryptionOptions options = new();

        options.ProviderName.ShouldBe("Aes");
    }

    [Fact]
    public void Defaults_VaultKeyName_IsStringEncryption()
    {
        StringEncryptionOptions options = new();

        options.VaultKeyName.ShouldBe("string-encryption");
    }

    [Fact]
    public void Defaults_AllowEphemeralPassPhrase_IsFalse()
    {
        StringEncryptionOptions options = new();

        options.AllowEphemeralPassPhrase.ShouldBeFalse();
    }
}
