using System.Diagnostics.Metrics;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Internal;
using Granit.Privacy.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport.Internal;

public sealed class PrivacyScopeResolverTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;

    public PrivacyScopeResolverTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new PrivacyMetrics(_sp.GetRequiredService<IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    private static PrivacyExportContext Ctx() =>
        new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: Guid.NewGuid(),
            CallerUserId: Guid.NewGuid(),
            TenantId: null,
            Regulation: "EU_GDPR");

    private static ProviderRegistration Registration(
        string name,
        bool hasData = true,
        string? featureName = null) =>
        new(
            ProviderName: name,
            DisplayKey: $"Privacy.Scopes.{name}",
            FeatureName: featureName,
            HasDataProbe: (_, _, _) => ValueTask.FromResult(hasData));

    [Fact]
    public async Task ListVisibleAsync_ReturnsEveryRegisteredProvider_WhenAllGatesPass()
    {
        DataProviderRegistry registry = new();
        registry.Register(Registration("identity-local"));
        registry.Register(Registration("auditing"));
        registry.Register(Registration("documents"));

        PrivacyScopeResolver sut = new(registry, new AllowAllPrivacyScopeVisibilityPolicy(), _sp, _metrics);

        IReadOnlyList<ProviderDescriptor> result = await sut.ListVisibleAsync(Ctx(), TestContext.Current.CancellationToken);

        result.Select(d => d.ProviderName).ShouldBe(["identity-local", "auditing", "documents"], ignoreOrder: true);
    }

    [Fact]
    public async Task ListVisibleAsync_HidesProvider_WhenHasDataIsFalse()
    {
        DataProviderRegistry registry = new();
        registry.Register(Registration("identity-local", hasData: true));
        registry.Register(Registration("documents", hasData: false));

        PrivacyScopeResolver sut = new(registry, new AllowAllPrivacyScopeVisibilityPolicy(), _sp, _metrics);

        IReadOnlyList<ProviderDescriptor> result = await sut.ListVisibleAsync(Ctx(), TestContext.Current.CancellationToken);

        result.Select(d => d.ProviderName).ShouldBe(["identity-local"]);
    }

    [Fact]
    public async Task ListVisibleAsync_HidesProvider_WhenPolicyDenies()
    {
        DataProviderRegistry registry = new();
        registry.Register(Registration("identity-local"));
        registry.Register(Registration("documents"));

        DenyingPolicy policy = new("documents");

        PrivacyScopeResolver sut = new(registry, policy, _sp, _metrics);

        IReadOnlyList<ProviderDescriptor> result = await sut.ListVisibleAsync(Ctx(), TestContext.Current.CancellationToken);

        result.Select(d => d.ProviderName).ShouldBe(["identity-local"]);
    }

    private sealed class DenyingPolicy(string deniedProviderName) : IPrivacyScopeVisibilityPolicy
    {
        public ValueTask<bool> IsVisibleAsync(
            ProviderDescriptor descriptor,
            PrivacyExportContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(descriptor.ProviderName != deniedProviderName);
    }

    [Fact]
    public async Task ListVisibleAsync_ReturnsEmpty_WhenNoProvidersRegistered()
    {
        DataProviderRegistry registry = new();
        PrivacyScopeResolver sut = new(registry, new AllowAllPrivacyScopeVisibilityPolicy(), _sp, _metrics);

        IReadOnlyList<ProviderDescriptor> result = await sut.ListVisibleAsync(Ctx(), TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListVisibleAsync_PreservesFeatureNameOnDescriptor()
    {
        DataProviderRegistry registry = new();
        registry.Register(Registration("documents", featureName: "Documents.Privacy"));

        PrivacyScopeResolver sut = new(registry, new AllowAllPrivacyScopeVisibilityPolicy(), _sp, _metrics);

        IReadOnlyList<ProviderDescriptor> result = await sut.ListVisibleAsync(Ctx(), TestContext.Current.CancellationToken);

        result.Single().FeatureName.ShouldBe("Documents.Privacy");
        result.Single().DisplayKey.ShouldBe("Privacy.Scopes.documents");
    }

    [Fact]
    public async Task ListVisibleAsync_LegacyNameOnlyRegistration_IsVisibleByDefault()
    {
        // Registering by name only (legacy / test path) should still produce a visible scope
        // — the registry fills in a fail-open HasData probe.
        DataProviderRegistry registry = new();
        registry.Register("legacy-provider");

        PrivacyScopeResolver sut = new(registry, new AllowAllPrivacyScopeVisibilityPolicy(), _sp, _metrics);

        IReadOnlyList<ProviderDescriptor> result = await sut.ListVisibleAsync(Ctx(), TestContext.Current.CancellationToken);

        ProviderDescriptor descriptor = result.ShouldHaveSingleItem();
        descriptor.ProviderName.ShouldBe("legacy-provider");
        descriptor.DisplayKey.ShouldBe("legacy-provider");
        descriptor.FeatureName.ShouldBeNull();
    }

    [Fact]
    public async Task ListVisibleAsync_PassesProbeArgumentsThrough()
    {
        PrivacyExportContext probedContext = null!;
        ProviderRegistration probing = new(
            ProviderName: "probe",
            DisplayKey: "Privacy.Scopes.probe",
            FeatureName: null,
            HasDataProbe: (sp, ctx, _) =>
            {
                probedContext = ctx;
                sp.ShouldNotBeNull();
                return ValueTask.FromResult(true);
            });

        DataProviderRegistry registry = new();
        registry.Register(probing);

        PrivacyScopeResolver sut = new(registry, new AllowAllPrivacyScopeVisibilityPolicy(), _sp, _metrics);

        PrivacyExportContext given = Ctx();
        await sut.ListVisibleAsync(given, TestContext.Current.CancellationToken);

        probedContext.ShouldBeEquivalentTo(given);
    }
}
