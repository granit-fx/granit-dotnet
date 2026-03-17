using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Extensions;
using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyServiceCollectionExtensionsTests
{
    // -------------------------------------------------------------------------
    // AddGranitIdempotency(IConfigurationSection)
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitIdempotency_WithConfigSection_RegistersOptions()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Idempotency:HeaderName"] = "X-Idempotency-Key",
                ["Idempotency:KeyPrefix"] = "myapp",
            })
            .Build();

        services.AddGranitIdempotency(config.GetSection("Idempotency"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<IdempotencyOptions>));
    }

    // -------------------------------------------------------------------------
    // AddGranitIdempotency(Action<IdempotencyOptions>?)
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitIdempotency_WithDelegate_RegistersOptions()
    {
        ServiceCollection services = new();

        services.AddGranitIdempotency(opts => opts.HeaderName = "X-Custom");

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<IdempotencyOptions>));
    }

    [Fact]
    public void AddGranitIdempotency_WithoutDelegate_UsesDefaults()
    {
        ServiceCollection services = new();

        services.AddGranitIdempotency();

        services.ShouldNotBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Core registrations
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitIdempotency_RegistersOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitIdempotency();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<IdempotencyOptions>) &&
            d.ImplementationType == typeof(IdempotencyOptionsValidator) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitIdempotency_RegistersIdempotencyStore_Scoped()
    {
        ServiceCollection services = new();
        services.AddGranitIdempotency();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IIdempotencyStore) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitIdempotency_RegistersRecyclableMemoryStreamManager_Singleton()
    {
        ServiceCollection services = new();
        services.AddGranitIdempotency();

        services.ShouldContain(d =>
            d.ServiceType == typeof(RecyclableMemoryStreamManager) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitIdempotency_RegistersMiddleware_Transient()
    {
        ServiceCollection services = new();
        services.AddGranitIdempotency();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IdempotencyMiddleware) &&
            d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void AddGranitIdempotency_RecyclableMemoryStreamManager_NotDuplicated()
    {
        ServiceCollection services = new();
        services.AddSingleton<RecyclableMemoryStreamManager>(); // pre-register
        services.AddGranitIdempotency();

        services.Count(d => d.ServiceType == typeof(RecyclableMemoryStreamManager))
                .ShouldBe(1, "TryAddSingleton should not add a duplicate");
    }
}
