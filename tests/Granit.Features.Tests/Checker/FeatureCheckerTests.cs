using System.Diagnostics.Metrics;
using Granit.Features.Definitions;
using Granit.Features.Diagnostics;
using Granit.Features.Exceptions;
using Granit.Features.Internal;
using Granit.Features.ValueProviders;
using Granit.Features.ValueTypes;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Features.Tests.Checker;

public sealed class FeatureCheckerTests
{
    /// <summary>
    /// Creates a real in-memory FusionCache that acts as a pass-through (short TTL, no L2).
    /// </summary>
    private static FusionCache CreateNoopCache() => new(new FusionCacheOptions());

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

    private static ServiceProvider BuildServiceProvider(ICurrentTenant? currentTenant = null)
    {
        ServiceCollection sc = new();
        if (currentTenant is not null)
        {
            sc.AddSingleton(currentTenant);
        }

        return sc.BuildServiceProvider();
    }

    private static FeaturesMetrics CreateTestMetrics() =>
        new(new TestMeterFactory());

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];
        public Meter Create(MeterOptions options) { Meter m = new(options); _meters.Add(m); return m; }
        public void Dispose() { foreach (Meter m in _meters) { m.Dispose(); } }
    }

    private static FeatureChecker BuildChecker(
        IFeatureDefinitionStore store,
        ICurrentTenant? currentTenant,
        params IFeatureValueProvider[] providers)
    {
        ServiceProvider sp = BuildServiceProvider(currentTenant);
        return new(store, providers, sp, CreateNoopCache(), CreateTestMetrics());
    }

    // -------------------------------------------------------------------------
    // IsEnabledAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IsEnabledAsync_DefaultTrue_Returns_True()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(new FeatureDefinition("App.Feature", "true", FeatureValueType.Toggle));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        bool result = await checker.IsEnabledAsync("App.Feature", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_DefaultFalse_Returns_False()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(new FeatureDefinition("App.Feature", "false", FeatureValueType.Toggle));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        bool result = await checker.IsEnabledAsync("App.Feature", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // GetNumericAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetNumericAsync_ReturnsDefaultNumericValue()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.MaxPatients").Returns(new FeatureDefinition("App.MaxPatients", "50", FeatureValueType.Numeric));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        long result = await checker.GetNumericAsync("App.MaxPatients", TestContext.Current.CancellationToken);

        result.ShouldBe(50);
    }

    // -------------------------------------------------------------------------
    // RequireEnabledAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequireEnabledAsync_FeatureEnabled_DoesNotThrow()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(new FeatureDefinition("App.Feature", "true", FeatureValueType.Toggle));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        Func<Task> act = () => checker.RequireEnabledAsync("App.Feature", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task RequireEnabledAsync_FeatureDisabled_Throws_FeatureNotEnabledException()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(new FeatureDefinition("App.Feature", "false", FeatureValueType.Toggle));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        Func<Task> act = () => checker.RequireEnabledAsync("App.Feature", TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<FeatureNotEnabledException>(act)).Message.ShouldContain("App.Feature");
    }

    // -------------------------------------------------------------------------
    // Cascade: Tenant overrides Default
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TenantOverride_Takes_Precedence_Over_Default()
    {
        var tenantId = Guid.NewGuid();
        InMemoryFeatureStore featureStore = new();
        await featureStore.SetAsync("App.VideoConsultation", tenantId.ToString(), "true",
            TestContext.Current.CancellationToken);

        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.VideoConsultation")
            .Returns(new FeatureDefinition("App.VideoConsultation", "false", FeatureValueType.Toggle));

        ICurrentTenant currentTenant = WithTenant(tenantId);
        ServiceProvider sp = BuildServiceProvider(currentTenant);
        TenantFeatureValueProvider tenantProvider = new(sp, featureStore);
        FeatureChecker checker = new(store,
            [tenantProvider, new DefaultValueFeatureValueProvider()],
            sp,
            CreateNoopCache(),
            CreateTestMetrics());

        bool result = await checker.IsEnabledAsync("App.VideoConsultation", TestContext.Current.CancellationToken);

        result.ShouldBeTrue("tenant override is 'true' even though default is 'false'");
    }

    [Fact]
    public async Task NoTenantContext_Falls_Back_To_Default()
    {
        InMemoryFeatureStore featureStore = new();
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.VideoConsultation")
            .Returns(new FeatureDefinition("App.VideoConsultation", "false", FeatureValueType.Toggle));

        ICurrentTenant noTenant = NoTenant();
        ServiceProvider sp = BuildServiceProvider(noTenant);
        TenantFeatureValueProvider tenantProvider = new(sp, featureStore);
        FeatureChecker checker = new(store,
            [tenantProvider, new DefaultValueFeatureValueProvider()],
            sp,
            CreateNoopCache(),
            CreateTestMetrics());

        bool result = await checker.IsEnabledAsync("App.VideoConsultation", TestContext.Current.CancellationToken);

        result.ShouldBeFalse("no tenant context, falls back to default 'false'");
    }

    // -------------------------------------------------------------------------
    // Optional multi-tenancy: ICurrentTenant not registered
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NoCurrentTenant_Registered_FallsBack_To_Default()
    {
        InMemoryFeatureStore featureStore = new();
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.VideoConsultation")
            .Returns(new FeatureDefinition("App.VideoConsultation", "false", FeatureValueType.Toggle));

        // No ICurrentTenant registered — simulates a single-tenant application
        ServiceProvider sp = BuildServiceProvider(currentTenant: null);
        TenantFeatureValueProvider tenantProvider = new(sp, featureStore);
        FeatureChecker checker = new(store,
            [tenantProvider, new DefaultValueFeatureValueProvider()],
            sp,
            CreateNoopCache(),
            CreateTestMetrics());

        bool result = await checker.IsEnabledAsync("App.VideoConsultation", TestContext.Current.CancellationToken);

        result.ShouldBeFalse("ICurrentTenant not registered, cascade falls through to default 'false'");
    }

    [Fact]
    public async Task NoCurrentTenant_Registered_TenantOverride_NotApplied()
    {
        var tenantId = Guid.NewGuid();
        InMemoryFeatureStore featureStore = new();
        await featureStore.SetAsync("App.VideoConsultation", tenantId.ToString(), "true",
            TestContext.Current.CancellationToken);

        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.VideoConsultation")
            .Returns(new FeatureDefinition("App.VideoConsultation", "false", FeatureValueType.Toggle));

        // No ICurrentTenant — tenant override in the store must be ignored
        ServiceProvider sp = BuildServiceProvider(currentTenant: null);
        TenantFeatureValueProvider tenantProvider = new(sp, featureStore);
        FeatureChecker checker = new(store,
            [tenantProvider, new DefaultValueFeatureValueProvider()],
            sp,
            CreateNoopCache(),
            CreateTestMetrics());

        bool result = await checker.IsEnabledAsync("App.VideoConsultation", TestContext.Current.CancellationToken);

        result.ShouldBeFalse("no ICurrentTenant registered, store override must not be resolved");
    }

    // -------------------------------------------------------------------------
    // GetNumericAsync — non-parseable value returns 0
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetNumericAsync_NonParseableValue_Returns_Zero()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.MaxPatients").Returns(
            new FeatureDefinition("App.MaxPatients", "not-a-number", FeatureValueType.Numeric));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        long result = await checker.GetNumericAsync("App.MaxPatients", TestContext.Current.CancellationToken);

        result.ShouldBe(0L, "non-parseable numeric value should return 0");
    }

    [Fact]
    public async Task GetNumericAsync_EmptyStringDefault_Returns_Zero()
    {
        // FeatureDefinition requires non-whitespace default, so simulate via a provider override.
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.MaxPatients").Returns(
            new FeatureDefinition("App.MaxPatients", "abc", FeatureValueType.Numeric));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        long result = await checker.GetNumericAsync("App.MaxPatients", TestContext.Current.CancellationToken);

        result.ShouldBe(0L);
    }

    // -------------------------------------------------------------------------
    // GetValueAsync — all providers return null, falls back to definition default
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetValueAsync_AllProvidersReturnNull_ReturnsDefinitionDefault()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(
            new FeatureDefinition("App.Feature", "fallback-default", FeatureValueType.Selection));

        // Provider that always returns null
        IFeatureValueProvider nullProvider = Substitute.For<IFeatureValueProvider>();
        nullProvider.Order.Returns(100);
        nullProvider.GetOrNullAsync(Arg.Any<FeatureDefinition>(), Arg.Any<CancellationToken>())
                    .Returns((string?)null);

        FeatureChecker checker = BuildChecker(store, NoTenant(), nullProvider);

        string result = await checker.GetValueAsync("App.Feature", TestContext.Current.CancellationToken);

        result.ShouldBe("fallback-default");
    }

    // -------------------------------------------------------------------------
    // GetValueAsync — provider ordering respected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetValueAsync_FirstProvider_WinsWhenItReturnsValue()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(
            new FeatureDefinition("App.Feature", "default", FeatureValueType.Toggle));

        IFeatureValueProvider highPriority = Substitute.For<IFeatureValueProvider>();
        highPriority.Order.Returns(100);
        highPriority.GetOrNullAsync(Arg.Any<FeatureDefinition>(), Arg.Any<CancellationToken>())
                    .Returns("high-priority-value");

        IFeatureValueProvider lowPriority = Substitute.For<IFeatureValueProvider>();
        lowPriority.Order.Returns(200);
        lowPriority.GetOrNullAsync(Arg.Any<FeatureDefinition>(), Arg.Any<CancellationToken>())
                   .Returns("low-priority-value");

        FeatureChecker checker = BuildChecker(store, NoTenant(), highPriority, lowPriority);

        string result = await checker.GetValueAsync("App.Feature", TestContext.Current.CancellationToken);

        result.ShouldBe("high-priority-value");
    }

    [Fact]
    public async Task GetValueAsync_SkipsNullProvider_UsesNextProvider()
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(
            new FeatureDefinition("App.Feature", "default", FeatureValueType.Toggle));

        IFeatureValueProvider nullProvider = Substitute.For<IFeatureValueProvider>();
        nullProvider.Order.Returns(100);
        nullProvider.GetOrNullAsync(Arg.Any<FeatureDefinition>(), Arg.Any<CancellationToken>())
                    .Returns((string?)null);

        IFeatureValueProvider valueProvider = Substitute.For<IFeatureValueProvider>();
        valueProvider.Order.Returns(200);
        valueProvider.GetOrNullAsync(Arg.Any<FeatureDefinition>(), Arg.Any<CancellationToken>())
                     .Returns("resolved-value");

        FeatureChecker checker = BuildChecker(store, NoTenant(), nullProvider, valueProvider);

        string result = await checker.GetValueAsync("App.Feature", TestContext.Current.CancellationToken);

        result.ShouldBe("resolved-value");
    }

    // -------------------------------------------------------------------------
    // IsEnabledAsync — case insensitive check
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public async Task IsEnabledAsync_CaseInsensitiveTrue_Returns_True(string trueValue)
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(
            new FeatureDefinition("App.Feature", trueValue, FeatureValueType.Toggle));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        bool result = await checker.IsEnabledAsync("App.Feature", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("anything-else")]
    public async Task IsEnabledAsync_NonTrueValue_Returns_False(string nonTrueValue)
    {
        IFeatureDefinitionStore store = Substitute.For<IFeatureDefinitionStore>();
        store.GetRequired("App.Feature").Returns(
            new FeatureDefinition("App.Feature", nonTrueValue, FeatureValueType.Toggle));
        FeatureChecker checker = BuildChecker(store, NoTenant(),
            new DefaultValueFeatureValueProvider());

        bool result = await checker.IsEnabledAsync("App.Feature", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }
}
