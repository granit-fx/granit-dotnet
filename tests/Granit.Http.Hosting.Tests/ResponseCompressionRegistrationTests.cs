using Granit.Http.Hosting.ResponseCompression.Extensions;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Hosting.Tests;

public sealed class ResponseCompressionRegistrationTests
{
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
