using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Granit.Vault.Aws.Options;
using Granit.Vault.Aws.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class AwsSecretsCredentialProviderTests
{
    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();

    [Fact]
    public async Task ExecuteAsync_ValidSecret_ObtainsCredentials()
    {
        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetSecretValueResponse
            {
                SecretString = """{"username":"db_user","password":"s3cr3t"}""",
                VersionId = "0123456789abcdef0123456789abcdef",
            }));

        using AwsSecretsCredentialProvider provider = CreateProvider("arn:aws:secretsmanager:eu-west-1:0:secret:db");

        await provider.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilReadyAsync(provider);
        await provider.StopAsync(TestContext.Current.CancellationToken);

        provider.IsReady.ShouldBeTrue();
        provider.Username.ShouldBe("db_user");
        provider.Password.ShouldBe("s3cr3t");
    }

    [Fact]
    public async Task ExecuteAsync_NoSecretArnConfigured_StaysNotReady()
    {
        using AwsSecretsCredentialProvider provider = CreateProvider(databaseSecretArn: null);

        await provider.StartAsync(TestContext.Current.CancellationToken);
        await provider.StopAsync(TestContext.Current.CancellationToken);

        provider.IsReady.ShouldBeFalse();
        await _secretsManager.DidNotReceiveWithAnyArgs()
            .GetSecretValueAsync(null!, TestContext.Current.CancellationToken);
    }

    private AwsSecretsCredentialProvider CreateProvider(string? databaseSecretArn)
    {
        AwsVaultOptions options = new()
        {
            DatabaseSecretArn = databaseSecretArn,
            RotationCheckIntervalMinutes = 60,
        };
        return new AwsSecretsCredentialProvider(
            _secretsManager,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<AwsSecretsCredentialProvider>.Instance);
    }

    private static async Task WaitUntilReadyAsync(AwsSecretsCredentialProvider provider)
    {
        for (int i = 0; i < 50 && !provider.IsReady; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
