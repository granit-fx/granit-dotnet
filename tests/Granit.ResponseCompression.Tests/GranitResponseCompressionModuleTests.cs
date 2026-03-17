using Granit.Core.Modularity;
using Granit.ResponseCompression.Extensions;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.ResponseCompression.Tests;

public sealed class GranitResponseCompressionModuleTests
{
    [Fact]
    public void GranitResponseCompressionModule_IsGranitModule() =>
        typeof(GranitResponseCompressionModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitResponseCompressionModule_IsSealed() =>
        typeof(GranitResponseCompressionModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void AddGranitResponseCompression_RegistersResponseCompressionOptionsConfigurator()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitResponseCompression();

        builder.Services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IConfigureOptions<ResponseCompressionOptions>));
    }

    [Fact]
    public void AddGranitResponseCompression_RegistersBrotliProviderOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitResponseCompression();

        builder.Services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IConfigureOptions<BrotliCompressionProviderOptions>));
    }

    [Fact]
    public void AddGranitResponseCompression_RegistersGzipProviderOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitResponseCompression();

        builder.Services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IConfigureOptions<GzipCompressionProviderOptions>));
    }
}
