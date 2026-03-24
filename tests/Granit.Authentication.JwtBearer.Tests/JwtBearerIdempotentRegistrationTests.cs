using Granit.Authentication.JwtBearer.Extensions;
using Granit.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Tests;

public sealed class JwtBearerIdempotentRegistrationTests
{
    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "https://auth.test.com/realms/test",
                ["Authentication:Audience"] = "test-client",
                ["Authentication:RequireHttpsMetadata"] = "false",
            })
            .Build();

    [Fact]
    public void AddGranitJwtBearer_CalledTwice_DoesNotDuplicateServices()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);

        // Act — call twice to test idempotency
        services.AddGranitJwtBearer();
        services.AddGranitJwtBearer();

        // Assert — ICurrentUserService should only be registered once
        int currentUserServiceCount = services
            .Count(d => d.ServiceType == typeof(ICurrentUserService));
        currentUserServiceCount.ShouldBe(1);
    }

    [Fact]
    public void AddGranitJwtBearer_ReturnsSameServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);

        // Act
        IServiceCollection returned = services.AddGranitJwtBearer();

        // Assert
        returned.ShouldBeSameAs(services);
    }
}
