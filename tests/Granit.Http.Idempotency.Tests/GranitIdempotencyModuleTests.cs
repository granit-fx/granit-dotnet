using Granit.Caching;
using Granit.Http.Idempotency.Internal;
using Granit.Modularity;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class GranitIdempotencyModuleTests
{
    // =========================================================================
    // Module type
    // =========================================================================

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitIdempotencyModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    // =========================================================================
    // DependsOn attributes
    // =========================================================================

    [Fact]
    public void Module_DependsOnGranitCachingModule()
    {
        var attributes = (DependsOnAttribute[])Attribute.GetCustomAttributes(
            typeof(GranitIdempotencyModule), typeof(DependsOnAttribute));

        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();

        dependedTypes.ShouldContain(typeof(GranitCachingModule));
    }

    [Fact]
    public void Module_DependsOn_removed_security_dependency()
    {
        var attributes = (DependsOnAttribute[])Attribute.GetCustomAttributes(
            typeof(GranitIdempotencyModule), typeof(DependsOnAttribute));

        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();

    }

    // =========================================================================
    // ConfigureServices
    // =========================================================================

    [Fact]
    public void ConfigureServices_RegistersMiddleware()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        GranitIdempotencyModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IdempotencyMiddleware) &&
            d.Lifetime == ServiceLifetime.Transient);
    }
}
