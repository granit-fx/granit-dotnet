using Granit.Authentication.JwtBearer.GoogleCloud.Authentication;
using Granit.Authentication.JwtBearer.GoogleCloud.Extensions;
using Granit.Authentication.JwtBearer.GoogleCloud.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.GoogleCloud.Tests;

public sealed class GoogleCloudAuthenticationServiceCollectionExtensionsTests
{
    private static ServiceCollection CreateServices(Dictionary<string, string?>? config = null)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? new Dictionary<string, string?>
            {
                ["GoogleCloudAuth:ProjectId"] = "my-project",
                ["GoogleCloudAuth:AdminRole"] = "super-admin",
            })
            .Build();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddAuthentication().AddJwtBearer();
        services.AddAuthorizationBuilder();

        return services;
    }

    [Fact]
    public void AddGranitGoogleCloudAuthentication_RegistersOptions()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitGoogleCloudAuthentication();
        ServiceProvider sp = services.BuildServiceProvider();

        GoogleCloudAuthenticationOptions opts = sp.GetRequiredService<IOptions<GoogleCloudAuthenticationOptions>>().Value;
        opts.ProjectId.ShouldBe("my-project");
        opts.AdminRole.ShouldBe("super-admin");
    }

    [Fact]
    public void AddGranitGoogleCloudAuthentication_PostConfiguresJwtBearer()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitGoogleCloudAuthentication();
        ServiceProvider sp = services.BuildServiceProvider();

        IOptionsMonitor<JwtBearerOptions> monitor = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions jwt = monitor.Get(JwtBearerDefaults.AuthenticationScheme);

        jwt.Authority.ShouldBe("https://securetoken.google.com/my-project");
        jwt.Audience.ShouldBe("my-project");
        jwt.TokenValidationParameters.NameClaimType.ShouldBe("email");
        jwt.TokenValidationParameters.ValidIssuer.ShouldBe("https://securetoken.google.com/my-project");
        jwt.TokenValidationParameters.ValidAudience.ShouldBe("my-project");
    }

    [Fact]
    public void AddGranitGoogleCloudAuthentication_RegistersClaimsTransformation()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitGoogleCloudAuthentication();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IClaimsTransformation) &&
                 d.ImplementationType == typeof(GoogleCloudClaimsTransformation));
        descriptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitGoogleCloudAuthentication_RegistersAdminPolicy()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitGoogleCloudAuthentication();
        ServiceProvider sp = services.BuildServiceProvider();

        IOptions<AuthorizationOptions> authOpts = sp.GetRequiredService<IOptions<AuthorizationOptions>>();
        AuthorizationPolicy? policy = authOpts.Value.GetPolicy("Admin");
        policy.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitGoogleCloudAuthentication_ReturnsServiceCollection()
    {
        ServiceCollection services = CreateServices();

        IServiceCollection result = services.AddGranitGoogleCloudAuthentication();

        result.ShouldBeSameAs(services);
    }
}
