using Granit.Features.Definitions;
using Granit.Features.Internal;
using Granit.Features.ValueProviders;
using Granit.Features.ValueTypes;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests.ValueProviders;

public sealed class TenantFeatureValueProviderTests
{
    private static FeatureDefinition MakeDefinition(string name = "App.Feature") =>
        new(name, "false", FeatureValueType.Toggle);

    private static TenantFeatureValueProvider BuildProvider(
        ICurrentTenant? currentTenant = null,
        IFeatureStoreReader? featureStoreReader = null)
    {
        ServiceCollection sc = new();
        if (currentTenant is not null)
        {
            sc.AddSingleton(currentTenant);
        }

        ServiceProvider sp = sc.BuildServiceProvider();
        return new TenantFeatureValueProvider(sp, featureStoreReader ?? new InMemoryFeatureStore());
    }

    private static ICurrentTenant NoTenant()
    {
        ICurrentTenant cancellationToken = Substitute.For<ICurrentTenant>();
        cancellationToken.IsAvailable.Returns(false);
        cancellationToken.Id.Returns((Guid?)null);
        return cancellationToken;
    }

    private static ICurrentTenant WithTenant(Guid tenantId)
    {
        ICurrentTenant cancellationToken = Substitute.For<ICurrentTenant>();
        cancellationToken.IsAvailable.Returns(true);
        cancellationToken.Id.Returns(tenantId);
        return cancellationToken;
    }

    // -------------------------------------------------------------------------
    // Metadata
    // -------------------------------------------------------------------------

    [Fact]
    public void Name_Is_Tenant() =>
        BuildProvider().Name.ShouldBe("Tenant");

    [Fact]
    public void Order_Is_100() =>
        BuildProvider().Order.ShouldBe(100);

    // -------------------------------------------------------------------------
    // GetOrNullAsync — ICurrentTenant not registered (optional multi-tenancy)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrNullAsync_NoCurrentTenantRegistered_ReturnsNull()
    {
        TenantFeatureValueProvider provider = BuildProvider(currentTenant: null);

        string? result = await provider.GetOrNullAsync(
            MakeDefinition(), TestContext.Current.CancellationToken);

        result.ShouldBeNull("ICurrentTenant is not registered in the service provider");
    }

    // -------------------------------------------------------------------------
    // GetOrNullAsync — ICurrentTenant registered but no active tenant
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrNullAsync_NoActiveTenant_ReturnsNull()
    {
        TenantFeatureValueProvider provider = BuildProvider(currentTenant: NoTenant());

        string? result = await provider.GetOrNullAsync(
            MakeDefinition(), TestContext.Current.CancellationToken);

        result.ShouldBeNull("no tenant is active (IsAvailable = false)");
    }

    // -------------------------------------------------------------------------
    // GetOrNullAsync — tenant active, store lookup
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrNullAsync_TenantActive_NoStoreEntry_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        TenantFeatureValueProvider provider = BuildProvider(
            currentTenant: WithTenant(tenantId),
            featureStoreReader: new InMemoryFeatureStore());

        string? result = await provider.GetOrNullAsync(
            MakeDefinition(), TestContext.Current.CancellationToken);

        result.ShouldBeNull("no override stored for this tenant");
    }

    [Fact]
    public async Task GetOrNullAsync_TenantActive_StoreHasEntry_ReturnsValue()
    {
        var tenantId = Guid.NewGuid();
        InMemoryFeatureStore featureStore = new();
        await featureStore.SetAsync("App.Feature", tenantId.ToString(), "true",
            TestContext.Current.CancellationToken);

        TenantFeatureValueProvider provider = BuildProvider(
            currentTenant: WithTenant(tenantId),
            featureStoreReader: featureStore);

        string? result = await provider.GetOrNullAsync(
            MakeDefinition(), TestContext.Current.CancellationToken);

        result.ShouldBe("true");
    }

    [Fact]
    public async Task GetOrNullAsync_DifferentTenant_DoesNotReturnOtherTenantValue()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        InMemoryFeatureStore featureStore = new();
        await featureStore.SetAsync("App.Feature", tenantA.ToString(), "true",
            TestContext.Current.CancellationToken);

        // Active tenant is B, but override is stored for A
        TenantFeatureValueProvider provider = BuildProvider(
            currentTenant: WithTenant(tenantB),
            featureStoreReader: featureStore);

        string? result = await provider.GetOrNullAsync(
            MakeDefinition(), TestContext.Current.CancellationToken);

        result.ShouldBeNull("the active tenant (B) has no stored override");
    }
}
