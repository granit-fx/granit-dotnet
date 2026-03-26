using Granit.Diagnostics;
using Granit.Encryption.Diagnostics;
using Granit.Encryption.Options;
using Granit.Encryption.Providers;
using Granit.Encryption.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Encryption.Extensions;

/// <summary>
/// Extensions for registering encryption services in the DI container.
/// </summary>
public static class EncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Adds the string encryption service with the default AES-256-CBC provider.
    /// </summary>
    public static IServiceCollection AddGranitEncryption(
        this IServiceCollection services)
    {
        services
            .AddOptions<StringEncryptionOptions>()
            .BindConfiguration(StringEncryptionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IStringEncryptionProvider, AesStringEncryptionProvider>();

        services.TryAddSingleton<IStringEncryptionService, DefaultStringEncryptionService>();

        // In-memory fallback for dev/test — replaced by a Vault provider in production
        services.TryAddScoped<IEntityEncryptionKeyStore, InMemoryEntityEncryptionKeyStore>();

        // Crypto-shredding
        services.TryAddScoped<ICryptoShredder, DefaultCryptoShredder>();

        // Diagnostics
        services.TryAddSingleton<EncryptionMetrics>();
        GranitActivitySourceRegistry.Register(EncryptionActivitySource.Name);

        return services;
    }
}
