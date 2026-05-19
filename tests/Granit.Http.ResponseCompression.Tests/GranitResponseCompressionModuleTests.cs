using Granit.Http.ResponseCompression.Extensions;
using Granit.Modularity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ResponseCompression.Tests;

public sealed class GranitHttpResponseCompressionModuleTests
{
    [Fact]
    public void GranitHttpResponseCompressionModule_IsGranitModule() =>
        typeof(GranitHttpResponseCompressionModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitHttpResponseCompressionModule_IsSealed() =>
        typeof(GranitHttpResponseCompressionModule).IsSealed.ShouldBeTrue();

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
