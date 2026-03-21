using Granit.Vault.Azure.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Vault.Azure.Tests;

public sealed class VaultAzureActivitySourceAdditionalTests
{
    [Fact]
    public void Name_IsGranitVaultAzure() => VaultAzureActivitySource.Name.ShouldBe("Granit.Vault.Azure");

    [Fact]
    public void Operations_AkvGetKey_HasCorrectValue() => VaultAzureActivitySource.Operations.AkvGetKey.ShouldBe("akv.get-key");

    [Fact]
    public void Tags_KeyName_HasCorrectValue() => VaultAzureActivitySource.Tags.KeyName.ShouldBe("akv.key_name");

    [Fact]
    public void Tags_VaultUri_HasCorrectValue() => VaultAzureActivitySource.Tags.VaultUri.ShouldBe("akv.vault_uri");
}
