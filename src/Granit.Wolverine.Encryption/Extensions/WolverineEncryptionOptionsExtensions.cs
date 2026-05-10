using System.Text.Json.Serialization.Metadata;
using Granit.Encryption;
using Granit.Wolverine.Encryption.Internal;
using Wolverine;

namespace Granit.Wolverine.Encryption.Extensions;

/// <summary>
/// <see cref="WolverineOptions"/> extension for activating field-level encryption
/// of <see cref="EncryptedAttribute"/>-marked string properties on every message
/// and saga state Wolverine serializes through System.Text.Json.
/// </summary>
public static class WolverineEncryptionOptionsExtensions
{
    /// <summary>
    /// Switches Wolverine's serializer to System.Text.Json (if not already) and
    /// installs a type-info modifier that attaches
    /// <c>EncryptedStringJsonConverter</c> to every <see cref="string"/>
    /// property carrying <see cref="EncryptedAttribute"/>. Saga state, command
    /// payloads and integration events all flow through this configured
    /// pipeline, so PII fields end up as ciphertext on the outbox / saga store.
    /// </summary>
    /// <param name="options">The Wolverine options instance.</param>
    /// <param name="encryption">
    /// The encryption service used by the converter. Resolve from the host's
    /// service collection during Wolverine configuration:
    /// <c>encryption: app.Services.GetRequiredService&lt;IStringEncryptionService&gt;()</c>.
    /// </param>
    /// <returns>The options instance for chaining.</returns>
    /// <remarks>
    /// Existing rows in the outbox / saga store with plaintext values for what
    /// are now <c>[Encrypted]</c> properties remain readable: the converter
    /// returns values that don't carry the <c>enc:v1:</c> prefix verbatim.
    /// On the next write the row is rewritten with the prefix and ciphertext.
    /// </remarks>
    public static WolverineOptions UseEncryptedSensitiveData(
        this WolverineOptions options,
        IStringEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(encryption);

        EncryptedPropertyJsonTypeInfoModifier modifier = new(encryption);

        options.UseSystemTextJsonForSerialization(jsonOptions =>
        {
            DefaultJsonTypeInfoResolver resolver = new();
            resolver.Modifiers.Add(modifier.Modify);
            jsonOptions.TypeInfoResolver = resolver;
        });

        return options;
    }
}
