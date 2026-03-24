// =============================================================================
// Tests - GranitCachingRedisModule
// =============================================================================
// Vérifie que le module Redis s'initialise correctement
// et que les dépendances de modules sont respectées.
// =============================================================================

using Granit.Caching.StackExchangeRedis.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Caching.StackExchangeRedis.Tests;

public sealed class GranitCachingRedisModuleTests
{
    [Fact]
    public void ConfigureServices_IsEnabledFalse_DoesNotThrow()
    {
        // Arrange — Redis désactivé : aucun serveur Redis requis
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Cache:Redis:IsEnabled"] = "false";

        // Enregistrement manuel des dépendances (GranitCachingModule d'abord)
        GranitCachingModule cachingModule = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        cachingModule.ConfigureServices(context);

        GranitCachingRedisModule redisModule = new();

        // Act
        Action act = () => redisModule.ConfigureServices(context);

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void ConfigureServices_IsEnabledTrue_RegistersRedisCachingOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Cache:Redis:IsEnabled"] = "true";
        builder.Configuration["Cache:Redis:Configuration"] = "localhost:6379";
        builder.Configuration["Cache:Redis:InstanceName"] = "test:";

        GranitCachingModule cachingModule = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        cachingModule.ConfigureServices(context);

        GranitCachingRedisModule redisModule = new();

        // Act
        redisModule.ConfigureServices(context);

        // Assert — RedisCachingOptions doit être enregistré
        ServiceDescriptor? optsDescriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType.IsGenericType
                 && d.ServiceType.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Options.IConfigureOptions<>)
                 && d.ServiceType.GetGenericArguments()[0] == typeof(RedisCachingOptions));
        optsDescriptor.ShouldNotBeNull();
    }
}
