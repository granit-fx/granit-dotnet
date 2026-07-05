using Granit.Presence.Abstractions;
using Granit.Presence.EntityFrameworkCore.Extensions;
using Granit.Presence.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Presence.EntityFrameworkCore.Tests;

/// <summary>
/// Regression: <c>EfPresenceStore</c> consumes <c>IDbContextFactory&lt;PresenceDbContext&gt;</c>,
/// which <c>AddGranitDbContext</c> registers as Scoped (the EF interceptors capture Scoped
/// services such as <c>ICurrentTenant</c>). Registering <c>EfPresenceStore</c> as Singleton
/// triggered a captive-dependency failure on host boot in any consumer running with
/// <c>ValidateScopes</c> / <c>ValidateOnBuild</c> enabled.
/// </summary>
public sealed class PresenceScopeValidationTests
{
    [Fact]
    public void AddGranitPresenceEntityFrameworkCore_should_register_IPresenceStore_as_Scoped()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Configuration.AddInMemoryCollection();

        builder.AddGranitPresenceEntityFrameworkCore(opts => opts.UseInMemoryDatabase("scope-validation-test"));

        ServiceDescriptor storeDescriptor = builder.Services
            .Single(s => s.ServiceType == typeof(IPresenceStore));

        storeDescriptor.Lifetime.ShouldBe(
            ServiceLifetime.Scoped,
            "IPresenceStore must be Scoped because EfPresenceStore consumes the Scoped " +
            $"IDbContextFactory<PresenceDbContext>. Got {storeDescriptor.Lifetime}.");

        ServiceDescriptor concreteDescriptor = builder.Services
            .Single(s => s.ServiceType == typeof(EfPresenceStore));

        concreteDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
