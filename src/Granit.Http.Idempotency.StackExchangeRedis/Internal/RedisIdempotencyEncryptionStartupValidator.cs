using Granit.Caching.Options;
using Granit.Http.Idempotency.StackExchangeRedis.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Http.Idempotency.StackExchangeRedis.Internal;

/// <summary>
/// Fail-closed startup validator: outside Development, refuses to start when idempotency
/// entries would be written to Redis in plaintext (no <c>Cache:Encryption:Key</c> resolvable).
/// </summary>
/// <remarks>
/// Mirrors <c>RedisCacheEncryptionStartupValidator</c> in <c>Granit.Caching.StackExchangeRedis</c>
/// but is stricter: idempotency entries replay full responses (potential PII/bearer tokens) and
/// were always-encrypted under the former <c>[CacheEncrypted]</c> path, so there is no
/// "global flag off" escape hatch here — a key is simply required. Development stays permissive
/// (local Redis without a key is a common inner loop and holds no production data).
/// </remarks>
internal sealed class RedisIdempotencyEncryptionStartupValidator(
    IHostEnvironment environment,
    IOptions<CacheEncryptionOptions> encryptionOptions)
    : IValidateOptions<RedisIdempotencyOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisIdempotencyOptions options)
    {
        // Only the default (unnamed) options instance is validated — it arrives as
        // string.Empty through IOptionsFactory and as null through direct IOptions<T> resolution.
        if (!string.IsNullOrEmpty(name))
        {
            return ValidateOptionsResult.Skip;
        }

        if (!options.IsEnabled || environment.IsDevelopment())
        {
            return ValidateOptionsResult.Success;
        }

        // Reading Value forces any PostConfigure that hydrates the key (e.g. a Vault bridge)
        // to run first, so a Vault-sourced key is honoured before deciding the outcome.
        if (!string.IsNullOrWhiteSpace(encryptionOptions.Value.Key))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"The Redis idempotency store is active in the '{environment.EnvironmentName}' environment " +
            "but no Cache:Encryption:Key is resolvable. Idempotency entries replay full HTTP responses " +
            "(potential PII, bearer tokens) and must never sit in plaintext in a Redis snapshot or AOF " +
            "file. Provide a base64 256-bit Cache:Encryption:Key (via Vault, a Vault bridge package, or " +
            "a secure environment variable), or run in the Development environment where this check is " +
            "relaxed.");
    }
}
