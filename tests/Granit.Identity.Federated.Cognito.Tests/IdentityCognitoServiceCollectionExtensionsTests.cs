using Amazon.CognitoIdentityProvider;
using Granit.Events;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Cognito.Extensions;
using Granit.Identity.Federated.Cognito.Internal;
using Granit.Identity.Federated.Cognito.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

public sealed class IdentityCognitoServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        Dictionary<string, string?> config = new()
        {
            ["Identity:Federated:Cognito:Region"] = "eu-west-1",
            ["Identity:Federated:Cognito:UserPoolId"] = "eu-west-1_TEST",
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        services.AddGranitIdentity();
        services.AddGranitIdentityCognito();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddGranitIdentityCognito_RegistersIdentityProvider()
    {
        ServiceProvider provider = BuildProvider();

        IIdentityProvider identityProvider = provider.GetRequiredService<IIdentityProvider>();

        identityProvider.ShouldBeOfType<CognitoIdentityProvider>();
    }

    [Fact]
    public void AddGranitIdentityCognito_RegistersCapabilities()
    {
        ServiceProvider provider = BuildProvider();

        IIdentityProviderCapabilities capabilities = provider.GetRequiredService<IIdentityProviderCapabilities>();

        capabilities.ShouldBeOfType<CognitoIdentityProviderCapabilities>();
        capabilities.ProviderName.ShouldBe("Cognito");
    }

    [Fact]
    public void AddGranitIdentityCognito_RegistersCognitoClient()
    {
        ServiceProvider provider = BuildProvider();

        IAmazonCognitoIdentityProvider client = provider.GetRequiredService<IAmazonCognitoIdentityProvider>();

        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitIdentityCognito_RegistersOptions()
    {
        ServiceProvider provider = BuildProvider();

        CognitoAdminOptions options = provider.GetRequiredService<IOptions<CognitoAdminOptions>>().Value;

        options.Region.ShouldBe("eu-west-1");
        options.UserPoolId.ShouldBe("eu-west-1_TEST");
    }

    [Fact]
    public void AddGranitIdentityCognito_ReturnsSameCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitIdentityCognito();

        result.ShouldBeSameAs(services);
    }
}
