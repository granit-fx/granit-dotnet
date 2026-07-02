// =============================================================================
// Tests - RedisCacheEncryptionStartupValidator
// =============================================================================
// Fail-closed gate: outside Development, an active Redis L2 cache with no resolvable
// encryption key and EncryptValues off must refuse to start; Development and any
// key-or-flag-present configuration must pass.
// =============================================================================

using Granit.Caching.Options;
using Granit.Caching.StackExchangeRedis.Extensions;
using Granit.Caching.StackExchangeRedis.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Caching.StackExchangeRedis.Tests;

public sealed class RedisCacheEncryptionStartupValidatorTests
{
    private static IHostEnvironment Env(string environmentName)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return env;
    }

    private static RedisCacheEncryptionStartupValidator Validator(string environmentName, string? key) =>
        new(Env(environmentName), Microsoft.Extensions.Options.Options.Create(new CacheEncryptionOptions { Key = key }));

    [Fact]
    public void Production_EncryptionOff_NoKey_Fails()
    {
        RedisCacheEncryptionStartupValidator validator = Validator("Production", key: null);

        ValidateOptionsResult result = validator.Validate(null, new CachingOptions { EncryptValues = false });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("plaintext");
        result.FailureMessage.ShouldContain("Cache:Encryption:Key");
    }

    [Fact]
    public void Staging_EncryptionOff_NoKey_Fails()
    {
        // Any non-Development environment is gated, not just Production.
        RedisCacheEncryptionStartupValidator validator = Validator("Staging", key: null);

        ValidateOptionsResult result = validator.Validate(null, new CachingOptions { EncryptValues = false });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Staging");
    }

    [Fact]
    public void Development_EncryptionOff_NoKey_Succeeds()
    {
        // Development stays permissive: local Redis without a key is a normal inner-loop setup.
        RedisCacheEncryptionStartupValidator validator = Validator("Development", key: null);

        ValidateOptionsResult result = validator.Validate(null, new CachingOptions { EncryptValues = false });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Production_EncryptValuesTrue_Succeeds()
    {
        RedisCacheEncryptionStartupValidator validator = Validator("Production", key: null);

        ValidateOptionsResult result = validator.Validate(null, new CachingOptions { EncryptValues = true });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Production_KeyResolvable_EncryptValuesFalse_Succeeds()
    {
        // A key alone is enough: [CacheEncrypted] types encrypt even with the global flag off.
        RedisCacheEncryptionStartupValidator validator = Validator("Production", key: FakeBase64Key());

        ValidateOptionsResult result = validator.Validate(null, new CachingOptions { EncryptValues = false });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void NamedOptionsInstance_IsSkipped()
    {
        RedisCacheEncryptionStartupValidator validator = Validator("Production", key: null);

        ValidateOptionsResult result = validator.Validate("some-named-instance", new CachingOptions { EncryptValues = false });

        result.Skipped.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DefaultOptionsInstance_NullOrEmptyName_IsValidated(string? defaultName)
    {
        // IOptionsFactory passes the default name as string.Empty; direct IOptions<T> passes null.
        // Both must be treated as the default instance and validated, not skipped.
        RedisCacheEncryptionStartupValidator validator = Validator("Production", key: null);

        ValidateOptionsResult result = validator.Validate(defaultName, new CachingOptions { EncryptValues = false });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitCachingRedis_ArmsValidator_ProductionPlaintext_StartupThrows()
    {
        // End-to-end teeth: AddGranitCachingRedis must register the validator with ValidateOnStart,
        // so a Production host with Redis on and no encryption fails at BuildServiceProvider/start.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:Redis:Configuration"] = "localhost:6379",
                ["Cache:EncryptValues"] = "false",
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Env("Production"));
        services
            .AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations();
        services
            .AddOptions<CacheEncryptionOptions>()
            .BindConfiguration(CacheEncryptionOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddGranitCachingRedis();

        // The gate must be registered as an IValidateOptions<CachingOptions>.
        services.ShouldContain(
            d => d.ServiceType == typeof(IValidateOptions<CachingOptions>)
                 && d.ImplementationType == typeof(RedisCacheEncryptionStartupValidator));

        using ServiceProvider sp = services.BuildServiceProvider();

        // Building the options runs every IValidateOptions<CachingOptions>; the gate fails plaintext-Redis.
        IOptionsFactory<CachingOptions> factory = sp.GetRequiredService<IOptionsFactory<CachingOptions>>();
        Action forceValidation = () => _ = factory.Create(Microsoft.Extensions.Options.Options.DefaultName);
        OptionsValidationException ex = forceValidation.ShouldThrow<OptionsValidationException>();
        ex.Failures.ShouldContain(f => f.Contains("plaintext"));
    }

    [Fact]
    public void AddGranitCachingRedis_ArmsValidator_DevelopmentPlaintext_StartupSucceeds()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:Redis:Configuration"] = "localhost:6379",
                ["Cache:EncryptValues"] = "false",
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Env("Development"));
        services
            .AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations();
        services
            .AddOptions<CacheEncryptionOptions>()
            .BindConfiguration(CacheEncryptionOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddGranitCachingRedis();

        using ServiceProvider sp = services.BuildServiceProvider();

        IOptionsFactory<CachingOptions> factory = sp.GetRequiredService<IOptionsFactory<CachingOptions>>();
        Action forceValidation = () => _ = factory.Create(Microsoft.Extensions.Options.Options.DefaultName);
        forceValidation.ShouldNotThrow();
    }

    private static string FakeBase64Key()
    {
        byte[] keyBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(keyBytes);
        return Convert.ToBase64String(keyBytes);
    }
}
