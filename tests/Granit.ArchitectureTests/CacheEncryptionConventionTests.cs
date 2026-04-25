using System.Reflection;
using Granit.Caching;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces that types known to flow through the L2 cache (Redis via
/// <c>IFusionCache</c>) and that carry secrets / PII are annotated with
/// <see cref="CacheEncryptedAttribute"/>, so the cached payload is
/// AES-256-GCM encrypted regardless of the global
/// <c>CachingOptions.EncryptValues</c> flag.
/// </summary>
/// <remarks>
/// SECURITY: <see cref="CacheEncryptedAttribute"/> historically existed but
/// was never applied to any framework type. A Redis breach would expose BFF
/// OAuth tokens, DPoP private keys, and cached Vault secrets in plaintext.
/// This test is a positive allowlist: every cached "carries-secrets" type
/// MUST be enumerated AND must declare the attribute. Adding a new cached
/// type without listing it here passes the test only by accident — adding
/// it without the attribute fails immediately.
/// </remarks>
public sealed class CacheEncryptionConventionTests
{
    /// <summary>
    /// Types that the framework is known to store in <c>IFusionCache</c> and
    /// that carry secrets, OAuth tokens, or PII. Each entry must be annotated
    /// with <see cref="CacheEncryptedAttribute"/>.
    /// </summary>
    /// <remarks>
    /// When you add a new type to <c>IFusionCache.SetAsync&lt;T&gt;</c> that
    /// holds tokens, credentials, signing keys, or PII, append it here AND
    /// decorate the type with <c>[CacheEncrypted]</c>. The test fails on
    /// either omission.
    /// </remarks>
    private static readonly string[] CachedTypesCarryingSecrets =
    [
        "Granit.Bff.BffTokenSet",          // OAuth tokens + DPoP private key
        "Granit.Vault.SecretDescriptor",   // Vault-cached secret payload
    ];

    [Fact]
    public void All_known_cached_secret_types_are_annotated_with_CacheEncrypted()
    {
        // Load assemblies from the test output directory so types from packages
        // we don't directly reference (e.g. via using directives) are still
        // discoverable. AppDomain.CurrentDomain.GetAssemblies() only contains
        // assemblies the runtime has already chosen to load.
        string outputDir = Path.GetDirectoryName(typeof(CacheEncryptionConventionTests).Assembly.Location)!;

        IEnumerable<Assembly> assemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.EndsWith(".resources", StringComparison.Ordinal);
            })
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
            })
            .Where(a => a is not null)!
            .Cast<Assembly>();

        var typeIndex = assemblies
            .SelectMany(a =>
            {
                try { return a.GetTypes().AsEnumerable(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
                catch { return []; }
            })
            .Where(t => t.FullName is not null)
            .GroupBy(t => t.FullName!)
            .ToDictionary(g => g.Key, g => g.First());

        List<string> violations = [];

        foreach (string fullName in CachedTypesCarryingSecrets)
        {
            if (!typeIndex.TryGetValue(fullName, out Type? type))
            {
                violations.Add($"{fullName} (type not found — was it renamed or deleted?)");
                continue;
            }

            if (!type.IsDefined(typeof(CacheEncryptedAttribute), inherit: false))
            {
                violations.Add($"{fullName} (missing [CacheEncrypted])");
            }
        }

        violations.ShouldBeEmpty(
            "Types listed in CacheEncryptionConventionTests.CachedTypesCarryingSecrets "
            + "MUST be annotated with [CacheEncrypted] from Granit.Caching. Without the "
            + "attribute, a Redis snapshot/AOF/replica leak yields plaintext OAuth tokens, "
            + "DPoP private keys, and Vault secrets. Violators: "
            + string.Join(", ", violations));
    }
}
