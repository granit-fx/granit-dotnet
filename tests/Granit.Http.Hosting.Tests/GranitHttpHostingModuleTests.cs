using Granit.Http.Hosting.Cors.Extensions;
using Granit.Http.Hosting.Cors.Options;
using Granit.Modularity;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Hosting.Tests;

public sealed class GranitHttpHostingModuleTests
{
    [Fact]
    public void GranitHttpHostingModule_IsGranitModule() =>
        typeof(GranitHttpHostingModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitHttpHostingModule_IsSealed() =>
        typeof(GranitHttpHostingModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void AddGranitCors_RegistersCorsOptionsValidator()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitCors();

        builder.Services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IValidateOptions<GranitCorsOptions>));
    }

    [Fact]
    public void AddGranitCors_RegistersCorsPolicyConfigurator()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitCors();

        builder.Services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IConfigureOptions<CorsOptions>));
    }

    [Fact]
    public void AddGranitCors_RegistersCorsServices()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitCors();

        builder.Services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(ICorsService));
    }
}
