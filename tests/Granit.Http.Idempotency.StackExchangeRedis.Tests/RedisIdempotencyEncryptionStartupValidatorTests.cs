// =============================================================================
// Tests - RedisIdempotencyEncryptionStartupValidator
// =============================================================================
// Fail-closed matrix: outside Development, the Redis idempotency store refuses
// to start without a resolvable Cache:Encryption:Key (entries carry replayable
// responses — plaintext at rest is never acceptable in production).
// =============================================================================

using Granit.Caching.Options;
using Granit.Http.Idempotency.StackExchangeRedis.Internal;
using Granit.Http.Idempotency.StackExchangeRedis.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.StackExchangeRedis.Tests;

public sealed class RedisIdempotencyEncryptionStartupValidatorTests
{
    private static RedisIdempotencyEncryptionStartupValidator CreateValidator(
        string environmentName, string? key)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new RedisIdempotencyEncryptionStartupValidator(
            environment,
            Microsoft.Extensions.Options.Options.Create(new CacheEncryptionOptions { Key = key }));
    }

    private static string NewBase64Key() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Production_WithoutKey_Fails()
    {
        ValidateOptionsResult result = CreateValidator(Environments.Production, key: null)
            .Validate(null, new RedisIdempotencyOptions());

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotBeNull();
        result.FailureMessage.ShouldContain("Cache:Encryption:Key");
    }

    [Fact]
    public void Production_WithKey_Succeeds() =>
        CreateValidator(Environments.Production, NewBase64Key())
            .Validate(null, new RedisIdempotencyOptions())
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void Development_WithoutKey_Succeeds() =>
        CreateValidator(Environments.Development, key: null)
            .Validate(null, new RedisIdempotencyOptions())
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void Production_WithoutKey_DisabledStore_Succeeds() =>
        CreateValidator(Environments.Production, key: null)
            .Validate(null, new RedisIdempotencyOptions { IsEnabled = false })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void NamedOptionsInstance_IsSkipped()
    {
        ValidateOptionsResult result = CreateValidator(Environments.Production, key: null)
            .Validate("named", new RedisIdempotencyOptions());

        result.Skipped.ShouldBeTrue();
    }

    [Fact]
    public void EmptyName_IsTreatedAsDefaultInstance() =>
        // IOptionsFactory passes string.Empty for the default instance.
        CreateValidator(Environments.Production, key: null)
            .Validate(string.Empty, new RedisIdempotencyOptions())
            .Failed.ShouldBeTrue();
}
