using System.Reflection;
using Granit.Modularity;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Tests;

public sealed class ServiceConfigurationContextTests
{
    private static HostApplicationBuilder CreateBuilder() => Host.CreateEmptyApplicationBuilder(null);

    [Fact]
    public void Constructor_ExposesServices()
    {
        HostApplicationBuilder builder = CreateBuilder();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        context.Services.ShouldBeSameAs(builder.Services);
    }

    [Fact]
    public void Constructor_ExposesConfiguration()
    {
        HostApplicationBuilder builder = CreateBuilder();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        context.Configuration.ShouldBeSameAs(builder.Configuration);
    }

    [Fact]
    public void Constructor_ExposesBuilder()
    {
        HostApplicationBuilder builder = CreateBuilder();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        context.Builder.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Constructor_NullModuleAssemblies_ReturnsEmptyList()
    {
        HostApplicationBuilder builder = CreateBuilder();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        context.ModuleAssemblies.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithModuleAssemblies_ExposesAssemblies()
    {
        HostApplicationBuilder builder = CreateBuilder();
        IReadOnlyList<Assembly> assemblies = [typeof(ServiceConfigurationContext).Assembly];
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder, assemblies);

        context.ModuleAssemblies.ShouldBe(assemblies);
    }

    [Fact]
    public void Items_IsEmptyByDefault()
    {
        HostApplicationBuilder builder = CreateBuilder();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        context.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Items_CanStoreAndRetrieveValues()
    {
        HostApplicationBuilder builder = CreateBuilder();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        context.Items["key"] = "value";

        context.Items["key"].ShouldBe("value");
    }

    [Fact]
    public void Items_SupportsNullValues()
    {
        HostApplicationBuilder builder = CreateBuilder();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        context.Items["nullable"] = null;

        context.Items["nullable"].ShouldBeNull();
    }
}
