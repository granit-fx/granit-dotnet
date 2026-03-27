using System.Security.Cryptography;
using Granit.Encryption;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Post-configures <see cref="OpenIddictServerOptions"/> to load signing and encryption
/// credentials from the database instead of using ephemeral keys.
/// </summary>
/// <remarks>
/// <para>
/// Executes at first resolve of <see cref="IOptions{OpenIddictServerOptions}"/> — after
/// all <c>ConfigureServices</c> calls are complete and DI is fully built. This allows us
/// to resolve <see cref="ISigningKeyStore"/> and <see cref="IStringEncryptionService"/>
/// from the container.
/// </para>
/// <para>
/// Active keys are used for signing new tokens. Retired keys remain in the credentials
/// list for verification of existing tokens (grace period).
/// </para>
/// <para>
/// When key rotation is disabled (<see cref="GranitKeyRotationOptions.Enabled"/> = false),
/// this post-configure is a no-op — the server keeps its ephemeral development keys.
/// </para>
/// </remarks>
#pragma warning disable GRSEC003 // Key material handling, not hardcoded secrets
internal sealed partial class DatabaseSigningKeyPostConfigure(
    IServiceScopeFactory scopeFactory,
    IOptions<GranitKeyRotationOptions> rotationOptions,
    ILogger<DatabaseSigningKeyPostConfigure> logger)
    : IPostConfigureOptions<OpenIddictServerOptions>
{
    /// <inheritdoc/>
    public void PostConfigure(string? name, OpenIddictServerOptions options)
    {
        if (!rotationOptions.Value.Enabled)
        {
            return;
        }

        // Create a scope to resolve scoped services (ISigningKeyStore, IStringEncryptionService).
        // PostConfigure runs once at startup — acceptable blocking call.
        using IServiceScope scope = scopeFactory.CreateScope();
        ISigningKeyStore keyStore = scope.ServiceProvider.GetRequiredService<ISigningKeyStore>();
        IStringEncryptionService encryptionService = scope.ServiceProvider.GetRequiredService<IStringEncryptionService>();

        IReadOnlyList<SigningKey> keys = keyStore
            .GetKeysAsync(SigningKeyStatus.Active, SigningKeyStatus.Retired)
            .GetAwaiter().GetResult();

        if (keys.Count == 0)
        {
            Log.NoKeysFound(logger);
            return;
        }

        // Remove ephemeral development keys
        options.SigningCredentials.Clear();
        options.EncryptionCredentials.Clear();

        foreach (SigningKey key in keys)
        {
            RsaSecurityKey rsaKey = DeserializeKey(key, encryptionService);

            if (key.KeyType == "signing")
            {
                var signingCredentials = new SigningCredentials(rsaKey, key.Algorithm);
                options.SigningCredentials.Add(signingCredentials);
                Log.SigningKeyLoaded(logger, key.KeyId, key.Status);
            }
            else if (key.KeyType == "encryption")
            {
                var encryptingCredentials = new EncryptingCredentials(
                    rsaKey, SecurityAlgorithms.RsaOAEP, SecurityAlgorithms.Aes256CbcHmacSha512);
                options.EncryptionCredentials.Add(encryptingCredentials);
                Log.EncryptionKeyLoaded(logger, key.KeyId, key.Status);
            }
        }

        Log.KeysLoaded(logger, options.SigningCredentials.Count, options.EncryptionCredentials.Count);
    }

    private static RsaSecurityKey DeserializeKey(SigningKey key, IStringEncryptionService encryptionService)
    {
        string? decrypted = encryptionService.Decrypt(key.EncryptedKeyMaterial);
        if (string.IsNullOrEmpty(decrypted))
        {
            throw new InvalidOperationException(
                $"Failed to decrypt signing key '{key.KeyId}'. Check IStringEncryptionService configuration.");
        }

        byte[] keyBytes = Convert.FromBase64String(decrypted);
        try
        {
            var rsa = RSA.Create();
            try
            {
                rsa.ImportRSAPrivateKey(keyBytes, out _);
                return new RsaSecurityKey(rsa) { KeyId = key.KeyId };
            }
            catch
            {
                rsa.Dispose();
                throw;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Key rotation is enabled but no signing keys found in the database. " +
                      "Run the openiddict-key-rotation job or call IKeyRotationService.RotateAsync() to generate keys.")]
        public static partial void NoKeysFound(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Loaded signing key '{KeyId}' (status: {Status}).")]
        public static partial void SigningKeyLoaded(ILogger logger, string keyId, SigningKeyStatus status);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Loaded encryption key '{KeyId}' (status: {Status}).")]
        public static partial void EncryptionKeyLoaded(ILogger logger, string keyId, SigningKeyStatus status);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Loaded {SigningCount} signing credential(s) and {EncryptionCount} encryption credential(s) from database.")]
        public static partial void KeysLoaded(ILogger logger, int signingCount, int encryptionCount);
    }
}
#pragma warning restore GRSEC003
