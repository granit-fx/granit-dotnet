using Granit.Vault.HashiCorp.Options;
using Shouldly;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests;

public sealed class HashiCorpVaultOptionsTests
{
    [Fact]
    public void SectionName_IsVault() => HashiCorpVaultOptions.SectionName.ShouldBe("Vault");

    [Fact]
    public void Address_DefaultsToEmpty()
    {
        HashiCorpVaultOptions options = new();

        options.Address.ShouldBe(string.Empty);
    }

    [Fact]
    public void AuthMethod_DefaultsToKubernetes()
    {
        HashiCorpVaultOptions options = new();

        options.AuthMethod.ShouldBe("Kubernetes");
    }

    [Fact]
    public void Token_DefaultsToNull()
    {
        HashiCorpVaultOptions options = new();

        options.Token.ShouldBeNull();
    }

    [Fact]
    public void KubernetesRole_DefaultsToMyBackend()
    {
        HashiCorpVaultOptions options = new();

        options.KubernetesRole.ShouldBe("my-backend");
    }

    [Fact]
    public void KubernetesTokenPath_DefaultsToServiceAccountTokenPath()
    {
        HashiCorpVaultOptions options = new();

        options.KubernetesTokenPath.ShouldBe("/var/run/secrets/kubernetes.io/serviceaccount/token");
    }

    [Fact]
    public void DatabaseMountPoint_DefaultsToDatabase()
    {
        HashiCorpVaultOptions options = new();

        options.DatabaseMountPoint.ShouldBe("database");
    }

    [Fact]
    public void DatabaseRoleName_DefaultsToReadwrite()
    {
        HashiCorpVaultOptions options = new();

        options.DatabaseRoleName.ShouldBe("readwrite");
    }

    [Fact]
    public void TransitMountPoint_DefaultsToTransit()
    {
        HashiCorpVaultOptions options = new();

        options.TransitMountPoint.ShouldBe("transit");
    }

    [Fact]
    public void LeaseRenewalThreshold_DefaultsTo075()
    {
        HashiCorpVaultOptions options = new();

        options.LeaseRenewalThreshold.ShouldBe(0.75);
    }
}
