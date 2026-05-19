using Granit.Http.Cors.Extensions;
using Granit.Http.Cors.Options;
using Granit.Modularity;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cors.Tests;

public sealed class GranitHttpCorsModuleTests
{
    [Fact]
    public void GranitHttpCorsModule_IsGranitModule() =>
        typeof(GranitHttpCorsModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitHttpCorsModule_IsSealed() =>
        typeof(GranitHttpCorsModule).IsSealed.ShouldBeTrue();

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
