using Granit.Payments;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Payments.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests.HealthChecks;

public sealed class PaymentProviderHealthCheckTests
{
    private const string ProviderName = "contoso";
    private const string ApiKey = "sk_live_ultra_secret";

    private readonly IPaymentProvider _provider = Substitute.For<IPaymentProvider>();
    private readonly PaymentProviderHealthCheck _check;

    public PaymentProviderHealthCheckTests()
    {
        _provider.Name.Returns(ProviderName);
        _check = new PaymentProviderHealthCheck(_provider, TimeProvider.System);
    }

    private static PaymentMethodCatalogEntry Entry(string methodType) =>
        new(methodType, PaymentMethodCategory.Card, methodType, PaymentMethodCapability.Wildcard);

    private Task<HealthCheckResult> InvokeAsync() =>
        _check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task NonEmptyCatalog_IsHealthy()
    {
        _provider.GetCatalogAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PaymentMethodCatalogEntry>>([Entry("card")]));

        HealthCheckResult result = await InvokeAsync();

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task EmptyCatalog_IsDegraded()
    {
        _provider.GetCatalogAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PaymentMethodCatalogEntry>>([]));

        HealthCheckResult result = await InvokeAsync();

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain(ProviderName);
        result.Description.ShouldContain("empty");
    }

    [Fact]
    public async Task UnauthorizedAccess_IsUnhealthyWithAuthMessage()
    {
        _provider.GetCatalogAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("invalid API key " + ApiKey));

        HealthCheckResult result = await InvokeAsync();

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("auth failed");
        result.Description.ShouldNotContain(ApiKey, Case.Sensitive);
    }

    [Fact]
    public async Task OperationCanceled_IsUnhealthyWithTimeoutMessage()
    {
        _provider.GetCatalogAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        HealthCheckResult result = await InvokeAsync();

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("timed out");
    }

    [Fact]
    public async Task GenericException_IsUnhealthyExposingOnlyTypeName()
    {
        _provider.GetCatalogAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused to https://api.contoso.com/v1/methods"));

        HealthCheckResult result = await InvokeAsync();

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("unreachable");
        result.Description.ShouldContain(nameof(HttpRequestException));
        result.Description.ShouldNotContain("api.contoso.com", Case.Insensitive);
        result.Description.ShouldNotContain("connection refused", Case.Insensitive);
    }

    [Fact]
    public async Task ErrorMessages_NeverExposeApiKeyFragment()
    {
        _provider.GetCatalogAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException($"bearer {ApiKey} rejected"));

        HealthCheckResult result = await InvokeAsync();

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldNotContain(ApiKey, Case.Sensitive);
        result.Description.ShouldNotContain("sk_live", Case.Insensitive);
    }
}
