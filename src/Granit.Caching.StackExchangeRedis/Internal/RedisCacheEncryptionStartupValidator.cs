using Granit.Caching.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Caching.StackExchangeRedis.Internal;

/// <summary>
/// Fail-closed startup validator for the L2 (Redis) cache: outside Development, refuses to start
/// when the distributed cache would write cache values to Redis in plaintext.
/// </summary>
/// <remarks>
/// <para>
/// This validator only exists once <c>AddGranitCachingRedis</c> has run, so its mere registration
/// signals that an L2 distributed provider is active — the exact condition under which unencrypted
/// values are a data-at-rest exposure (Redis snapshot, AOF file, ACL bypass). It is the real
/// security control behind both secure-by-default findings: without it, a host could enable Redis
/// yet leave <see cref="CachingOptions.EncryptValues"/> off with no key, and even a
/// <c>[CacheEncrypted]</c> type (idempotency entries, BFF token sets) would silently fall through
/// to the no-op encryptor and land in Redis as plaintext.
/// </para>
/// <para>
/// Why a validator rather than defaulting <see cref="CachingOptions.EncryptValues"/> to <c>true</c>:
/// flipping the global default would crash every existing Redis host that has no
/// <c>Cache:Encryption:Key</c> configured — a silent, hard breaking change. Instead the encryptor
/// registration is already key-driven (AES whenever a 256-bit key resolves), and this validator
/// closes the remaining gap by making a plaintext-Redis production start a loud, actionable
/// failure instead of a warning that scrolls past in the logs.
/// </para>
/// <para>
/// Development stays permissive: local Redis without a key is a common inner-loop setup and does not
/// hold production data. The gate keys off <c>IHostEnvironment.IsDevelopment()</c>.
/// </para>
/// </remarks>
internal sealed class RedisCacheEncryptionStartupValidator(
    IHostEnvironment environment,
    IOptions<CacheEncryptionOptions> encryptionOptions)
    : IValidateOptions<CachingOptions>
{
    public ValidateOptionsResult Validate(string? name, CachingOptions options)
    {
        // Only the default (unnamed) options instance carries the cache-wide flag. The default name
        // arrives as string.Empty through IOptionsFactory and as null through direct IOptions<T>
        // resolution, so both must be treated as "the default instance".
        if (!string.IsNullOrEmpty(name))
        {
            return ValidateOptionsResult.Skip;
        }

        if (environment.IsDevelopment())
        {
            return ValidateOptionsResult.Success;
        }

        // Reading Value here forces any PostConfigure that hydrates the key (e.g. the Vault bridge)
        // to run first, so a Vault-sourced key is honoured before we decide the outcome.
        bool keyResolvable = !string.IsNullOrWhiteSpace(encryptionOptions.Value.Key);

        // Encryption is effectively on when the global flag is set OR a key is present (the key alone
        // is enough for [CacheEncrypted] types to encrypt). It is effectively off — plaintext at rest —
        // only when neither holds.
        if (options.EncryptValues || keyResolvable)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"The Redis L2 distributed cache is active in the '{environment.EnvironmentName}' environment " +
            "but cache value encryption is disabled: Cache:EncryptValues is false and no " +
            "Cache:Encryption:Key is resolvable. Cached values — including [CacheEncrypted] payloads " +
            "such as idempotency responses and BFF token sets — would be written to Redis in plaintext. " +
            "Set Cache:EncryptValues to true and provide a base64 256-bit Cache:Encryption:Key (via Vault, " +
            "a Vault bridge package, or a secure environment variable), or, if encryption at rest is " +
            "guaranteed elsewhere, run in the Development environment where this check is relaxed.");
    }
}
