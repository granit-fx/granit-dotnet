using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Granit.Identity.Federated.Cognito.HealthChecks;
using Granit.Identity.Federated.Cognito.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

public sealed class CognitoHealthCheckTests
{
    private static CognitoHealthCheck Build(IAmazonCognitoIdentityProvider client) =>
        new(client, Microsoft.Extensions.Options.Options.Create(new CognitoAdminOptions { Region = "eu-west-1", UserPoolId = "eu-west-1_TEST" }));

    [Fact]
    public async Task PoolReachable_ReturnsHealthy()
    {
        IAmazonCognitoIdentityProvider client = Substitute.For<IAmazonCognitoIdentityProvider>();
        client.ListUsersAsync(Arg.Any<ListUsersRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListUsersResponse());

        HealthCheckResult result = await Build(client).CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CredentialsRejected_ReturnsUnhealthy()
    {
        IAmazonCognitoIdentityProvider client = Substitute.For<IAmazonCognitoIdentityProvider>();
        client.ListUsersAsync(Arg.Any<ListUsersRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotAuthorizedException("denied"));

        HealthCheckResult result = await Build(client).CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldNotContain("denied");
    }

    [Fact]
    public async Task UnexpectedError_DoesNotLeakMessage()
    {
        IAmazonCognitoIdentityProvider client = Substitute.For<IAmazonCognitoIdentityProvider>();
        client.ListUsersAsync(Arg.Any<ListUsersRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection string secret=abc"));

        HealthCheckResult result = await Build(client).CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldNotContain("secret=abc");
    }
}
