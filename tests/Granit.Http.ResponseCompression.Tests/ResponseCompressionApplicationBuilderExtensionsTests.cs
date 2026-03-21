using Granit.Http.ResponseCompression.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.ResponseCompression.Tests;

public sealed class ResponseCompressionApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseGranitResponseCompression_DoesNotThrow()
    {
        ServiceCollection services = new();
        services.AddResponseCompression();
        ServiceProvider provider = services.BuildServiceProvider();
        ApplicationBuilder app = new(provider);

        Should.NotThrow(() => app.UseGranitResponseCompression());
    }

    [Fact]
    public void UseGranitResponseCompression_ReturnsAppBuilder()
    {
        ServiceCollection services = new();
        services.AddResponseCompression();
        ServiceProvider provider = services.BuildServiceProvider();
        ApplicationBuilder app = new(provider);

        IApplicationBuilder result = app.UseGranitResponseCompression();

        result.ShouldNotBeNull();
    }
}
