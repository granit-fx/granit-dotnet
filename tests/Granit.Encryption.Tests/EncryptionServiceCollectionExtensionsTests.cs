// =============================================================================
// EncryptionServiceCollectionExtensionsTests - DI registration tests
// =============================================================================
// Verifies:
//   - All expected services are registered with correct lifetimes
//   - TryAdd semantics do not replace existing registrations
//   - Method returns the same IServiceCollection for fluent chaining
// =============================================================================

using Granit.Encryption.Diagnostics;
using Granit.Encryption.Extensions;
using Granit.Encryption.Options;
using Granit.Encryption.Providers;
using Granit.Encryption.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests;

public sealed class EncryptionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitEncryption_Registers_AesProvider()
    {
        ServiceCollection services = new();
        services.AddGranitEncryption();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IStringEncryptionProvider));

        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(AesStringEncryptionProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitEncryption_Registers_DefaultStringEncryptionService()
    {
        ServiceCollection services = new();
        services.AddGranitEncryption();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IStringEncryptionService));

        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(DefaultStringEncryptionService));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitEncryption_Registers_StringEncryptionOptions()
    {
        ServiceCollection services = new();
        services.AddGranitEncryption();

        bool hasOptions = services.Any(
            d => d.ServiceType.IsGenericType
                 && d.ServiceType.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Options.IConfigureOptions<>)
                 && d.ServiceType.GetGenericArguments()[0] == typeof(StringEncryptionOptions));

        hasOptions.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitEncryption_TryAdd_DoesNotReplace_ExistingService()
    {
        ServiceCollection services = new();

        // Register a custom IStringEncryptionService first
        services.AddSingleton<IStringEncryptionService, DefaultStringEncryptionService>();

        // AddGranitEncryption uses TryAddSingleton — should not replace
        services.AddGranitEncryption();

        int count = services.Count(d => d.ServiceType == typeof(IStringEncryptionService));
        count.ShouldBe(1);
    }

    [Fact]
    public void AddGranitEncryption_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitEncryption();

        result.ShouldBeSameAs(services);
    }

    // ──── Additional registrations ────

    [Fact]
    public void AddGranitEncryption_Registers_InMemoryEntityEncryptionKeyStore()
    {
        ServiceCollection services = new();
        services.AddGranitEncryption();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEntityEncryptionKeyStore));

        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(InMemoryEntityEncryptionKeyStore));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitEncryption_Registers_DefaultCryptoShredder()
    {
        ServiceCollection services = new();
        services.AddGranitEncryption();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ICryptoShredder));

        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(DefaultCryptoShredder));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitEncryption_Registers_EncryptionMetrics()
    {
        ServiceCollection services = new();
        services.AddGranitEncryption();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(EncryptionMetrics));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitEncryption_TryAdd_DoesNotReplace_ExistingKeyStore()
    {
        ServiceCollection services = new();
        services.AddScoped<IEntityEncryptionKeyStore, InMemoryEntityEncryptionKeyStore>();

        services.AddGranitEncryption();

        int count = services.Count(d => d.ServiceType == typeof(IEntityEncryptionKeyStore));
        count.ShouldBe(1);
    }

    [Fact]
    public void AddGranitEncryption_TryAdd_DoesNotReplace_ExistingCryptoShredder()
    {
        ServiceCollection services = new();
        services.AddScoped<ICryptoShredder, DefaultCryptoShredder>();

        services.AddGranitEncryption();

        int count = services.Count(d => d.ServiceType == typeof(ICryptoShredder));
        count.ShouldBe(1);
    }
}
