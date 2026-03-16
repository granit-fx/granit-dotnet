using Granit.Caching.Options;
namespace Granit.Caching;

/// <summary>
/// Fine-grained control over AES-256 encryption for a specific cache item type.
/// Takes precedence over the global <see cref="CachingOptions.EncryptValues"/> flag.
/// </summary>
/// <remarks>
/// Priority logic:
/// <list type="table">
///   <listheader><term>Attribute</term><term>Global EncryptValues</term><term>Result</term></listheader>
///   <item><term><c>[CacheEncrypted]</c></term><term>any</term><term>Encryption ENABLED</term></item>
///   <item><term><c>[CacheEncrypted(false)]</c></term><term>any</term><term>Encryption DISABLED</term></item>
///   <item><term>No attribute</term><term><c>true</c></term><term>Encryption ENABLED</term></item>
///   <item><term>No attribute</term><term><c>false</c></term><term>Encryption DISABLED</term></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Always encrypted — ISO 27001 sensitive data
/// [CacheEncrypted]
/// public sealed class PatientCacheItem { ... }
///
/// // Never encrypted — explicit opt-out (public configuration data)
/// [CacheEncrypted(false)]
/// public sealed class AppConfigCacheItem { ... }
///
/// // Follows the global CachingOptions.EncryptValues flag
/// public sealed class UserPreferencesCacheItem { ... }
/// </code>
/// </example>
/// <param name="encrypt"><c>true</c> (default) to force encryption, <c>false</c> to disable it.</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CacheEncryptedAttribute(bool encrypt = true) : Attribute
{
    /// <summary><c>true</c> to force encryption, <c>false</c> to disable it.</summary>
    public bool Encrypt { get; } = encrypt;
}
