using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Diagnostics;
using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IO;

namespace Granit.Http.Idempotency.Extensions;

/// <summary>
/// Extension methods for registering Granit Idempotency services.
/// </summary>
public static class IdempotencyServiceCollectionExtensions
{
    /// <summary>
    /// Registers Granit Idempotency services using a configuration section.
    /// </summary>
    public static IServiceCollection AddGranitIdempotency(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        services
            .AddOptions<IdempotencyOptions>()
            .Bind(configurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddGranitIdempotencyCore();
    }

    /// <summary>
    /// Registers Granit Idempotency services, binding <see cref="IdempotencyOptions"/> from the
    /// <c>Http:Idempotency</c> configuration section. The optional delegate runs after binding
    /// and can override individual values.
    /// </summary>
    public static IServiceCollection AddGranitIdempotency(
        this IServiceCollection services,
        Action<IdempotencyOptions>? configure = null)
    {
        OptionsBuilder<IdempotencyOptions> optionsBuilder = services
            .AddOptions<IdempotencyOptions>()
            .BindConfiguration(IdempotencyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services.AddGranitIdempotencyCore();
    }

    private static IServiceCollection AddGranitIdempotencyCore(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<IdempotencyOptions>, IdempotencyOptionsValidator>();
        services.TryAddSingleton<IdempotencyMetrics>();

        // IConditionalCache provides the backend (InMemory default, Redis via Granit.Caching.StackExchangeRedis).
        services.TryAddScoped<IIdempotencyStore, ConditionalCacheIdempotencyStore>();

        // RecyclableMemoryStreamManager is thread-safe and should be a singleton
        services.TryAddSingleton<RecyclableMemoryStreamManager>();

        // IMiddleware pattern: AddTransient so scoped services (ICurrentUserService, ICurrentTenant)
        // are resolved from the request scope via IMiddlewareFactory.
        services.AddTransient<IdempotencyMiddleware>();

        return services;
    }
}
