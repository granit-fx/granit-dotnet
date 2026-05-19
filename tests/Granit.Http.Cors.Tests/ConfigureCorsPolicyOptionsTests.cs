using Granit.Http.Cors.Internal;
using Granit.Http.Cors.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Shouldly;
using Xunit;

namespace Granit.Http.Cors.Tests;

public sealed class ConfigureCorsPolicyOptionsTests
{
    [Fact]
    public void Configure_WithExplicitOrigins_SetsOriginsOnDefaultPolicy()
    {
        GranitCorsOptions granitOptions = new()
        {
            AllowedOrigins = ["https://app.example.com", "https://admin.example.com"],
        };
        ConfigureCorsPolicyOptions sut = new(Microsoft.Extensions.Options.Options.Create(granitOptions));
        CorsOptions corsOptions = new();

        sut.Configure(corsOptions);

        CorsPolicy? policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName);
        policy.ShouldNotBeNull();
        policy!.Origins.ShouldContain("https://app.example.com");
        policy.Origins.ShouldContain("https://admin.example.com");
        policy.AllowAnyOrigin.ShouldBeFalse();
    }

    [Fact]
    public void Configure_WithWildcardOrigin_SetsAllowAnyOrigin()
    {
        GranitCorsOptions granitOptions = new() { AllowedOrigins = ["*"] };
        ConfigureCorsPolicyOptions sut = new(Microsoft.Extensions.Options.Options.Create(granitOptions));
        CorsOptions corsOptions = new();

        sut.Configure(corsOptions);

        CorsPolicy? policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName);
        policy.ShouldNotBeNull();
        policy!.AllowAnyOrigin.ShouldBeTrue();
    }

    [Fact]
    public void Configure_SetsAllowAnyHeaderAndMethod()
    {
        GranitCorsOptions granitOptions = new()
        {
            AllowedOrigins = ["https://app.example.com"],
        };
        ConfigureCorsPolicyOptions sut = new(Microsoft.Extensions.Options.Options.Create(granitOptions));
        CorsOptions corsOptions = new();

        sut.Configure(corsOptions);

        CorsPolicy? policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName);
        policy.ShouldNotBeNull();
        policy!.AllowAnyHeader.ShouldBeTrue();
        policy.AllowAnyMethod.ShouldBeTrue();
    }

    [Fact]
    public void Configure_WithAllowCredentials_SetsSupportsCredentials()
    {
        GranitCorsOptions granitOptions = new()
        {
            AllowedOrigins = ["https://app.example.com"],
            AllowCredentials = true,
        };
        ConfigureCorsPolicyOptions sut = new(Microsoft.Extensions.Options.Options.Create(granitOptions));
        CorsOptions corsOptions = new();

        sut.Configure(corsOptions);

        CorsPolicy? policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName);
        policy.ShouldNotBeNull();
        policy!.SupportsCredentials.ShouldBeTrue();
    }

    [Fact]
    public void Configure_WithoutAllowCredentials_DoesNotSetSupportsCredentials()
    {
        GranitCorsOptions granitOptions = new()
        {
            AllowedOrigins = ["https://app.example.com"],
            AllowCredentials = false,
        };
        ConfigureCorsPolicyOptions sut = new(Microsoft.Extensions.Options.Options.Create(granitOptions));
        CorsOptions corsOptions = new();

        sut.Configure(corsOptions);

        CorsPolicy? policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName);
        policy.ShouldNotBeNull();
        policy!.SupportsCredentials.ShouldBeFalse();
    }
}
