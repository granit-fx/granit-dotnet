using Granit.Vault.Aws.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class VaultAwsActivitySourceAdditionalTests
{
    [Fact]
    public void Name_IsGranitVaultAws() => VaultAwsActivitySource.Name.ShouldBe("Granit.Vault.Aws");

    [Fact]
    public void Operations_KmsDescribeKey_HasCorrectValue() => VaultAwsActivitySource.Operations.KmsDescribeKey.ShouldBe("kms.describe-key");

    [Fact]
    public void Operations_SecretsCheck_HasCorrectValue() => VaultAwsActivitySource.Operations.SecretsCheck.ShouldBe("secrets.check-rotation");

    [Fact]
    public void Tags_KeyName_HasCorrectValue() => VaultAwsActivitySource.Tags.KeyName.ShouldBe("kms.key_name");

    [Fact]
    public void Tags_Region_HasCorrectValue() => VaultAwsActivitySource.Tags.Region.ShouldBe("aws.region");
}
