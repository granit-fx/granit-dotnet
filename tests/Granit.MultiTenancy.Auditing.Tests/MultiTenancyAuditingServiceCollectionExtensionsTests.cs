using Granit.MultiTenancy.Auditing.Extensions;
using Granit.MultiTenancy.Authorization;
using Granit.MultiTenancy.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Auditing.Tests;

public sealed class MultiTenancyAuditingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitHostImpersonationAuditing_ReplacesDefaultAuditWriter()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        // Stub the open generic to avoid pulling in the full Granit.Localization stack.
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(StubLocalizer<>));
        services.AddGranitMultiTenancy();

        services.AddGranitHostImpersonationAuditing();

        ServiceDescriptor descriptor = services
            .Last(d => d.ServiceType == typeof(IHostImpersonationAuditWriter));
        descriptor.ImplementationType.ShouldBe(typeof(AuditingHostImpersonationAuditWriter));
    }

    private sealed class StubLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name, resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
