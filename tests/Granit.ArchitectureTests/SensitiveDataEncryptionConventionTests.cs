using System.Reflection;
using Granit.DataProtection;
using Granit.Domain;
using Granit.Encryption;
using Granit.Events;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Bridges the two PII markers: classification (<see cref="SensitiveDataAttribute"/>)
/// and storage protection (<see cref="EncryptedAttribute"/>).
/// </summary>
/// <remarks>
/// <para>
/// Granit splits the markers on purpose: <c>[SensitiveData]</c> is consumed by
/// audit / log / AI / export paths to mask or omit values at runtime, while
/// <c>[Encrypted]</c> drives the EF Core value converter and the Wolverine
/// JSON resolver to encrypt the value at rest. The conjunction is the
/// security-critical case: a property classified as <c>Confidential</c> or
/// <c>Restricted</c> that ends up persisted plain-text on the database or
/// outbox is a GDPR Art. 32 / ISO 27001 A.8.2 violation.
/// </para>
/// <para>
/// The rule: every <see cref="string"/> property carrying
/// <c>[SensitiveData(Level &gt;= Confidential)]</c> on a <b>persisted type</b>
/// (entity, saga, integration event) must also carry <c>[Encrypted]</c>.
/// Transient DTOs (<c>*Request</c>, <c>*Response</c>) are exempt — they flow
/// through HTTP and never reach a persistence boundary.
/// </para>
/// </remarks>
public sealed class SensitiveDataEncryptionConventionTests
{
    /// <summary>
    /// Properties intentionally not <c>[Encrypted]</c>, with category and justification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[INFRA]</c> — already protected by another mechanism (one-way hash, externally
    /// encrypted before persistence). Adding <c>[Encrypted]</c> would be redundant and may
    /// break dedup/lookup paths.
    /// </para>
    /// <para>
    /// <c>[BACKLOG]</c> — used as an equality lookup key (login, dedup, push routing).
    /// AES-CBC with random IV makes equality lookups impossible. Migrating these requires
    /// the <see cref="Granit.Identity.Federated.Internal.IUserLookupHasher"/> pattern: a
    /// parallel deterministic-hash column for the index, with the original value
    /// <c>[Encrypted]</c>. Tracked per module — entries are removed as each module ships
    /// the lookup-hash refactor.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> Exemptions = new(StringComparer.Ordinal)
    {
        // [INFRA] — already protected
        "Granit.Authentication.ApiKeys.Domain.ApiKeyEntry.HashedKey",       // HMAC-SHA256 hash of the issued key; reversal infeasible. The hash itself is the lookup index.
        "Granit.OpenIddict.Domain.SigningKey.EncryptedKeyMaterial",         // ciphertext produced by IDataProtectionProvider before persistence.
        "Granit.Webhooks.Domain.WebhookSigningKey.ProtectedSecret",         // opaque protected value; format owned by IWebhookSecretProtector (rotates independently).
        "Granit.Webhooks.Domain.WebhookSubscription.SigningSecret",         // legacy column populated only via the same protector path as ProtectedSecret.

        // [BACKLOG] — equality-lookup key, needs companion lookup-hash column
        "Granit.Identity.Domain.User.Email",                                // FindByEmailAsync (login). Refactor: introduce User.EmailHash via IUserLookupHasher.
        "Granit.Identity.Domain.User.PhoneNumber",                          // potential SMS-OTP / passwordless lookup. Same pattern as Email.
        "Granit.Parties.Domain.PartyEmail.CanonicalEmail",                  // Tier1DeterministicMatcher dedup index.
        "Granit.Parties.Domain.PartyPhone.CanonicalNumber",                 // Tier1DeterministicMatcher dedup index.
        "Granit.Tax.Domain.ValidatedTaxId.TaxId",                           // EfValidatedTaxIdStore.FirstOrDefaultAsync(v => v.TaxId == taxId).
    };

    [Fact]
    public void Confidential_string_properties_on_persisted_types_must_be_Encrypted()
    {
        string outputDir = Path.GetDirectoryName(typeof(SensitiveDataEncryptionConventionTests).Assembly.Location)!;

        Assembly[] granitAssemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests"))
            .Select(TryLoadAssembly)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToArray();

        List<string> violations = [];

        foreach (Assembly assembly in granitAssemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.OfType<Type>().ToArray();
            }

            foreach (Type type in types)
            {
                if (!IsPersisted(type))
                {
                    continue;
                }

                if (IsTransientDto(type))
                {
                    continue;
                }

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (property.PropertyType != typeof(string))
                    {
                        continue;
                    }

                    SensitiveDataAttribute? sensitive = property.GetCustomAttribute<SensitiveDataAttribute>();
                    if (sensitive is null || sensitive.Level < Sensitivity.Confidential)
                    {
                        continue;
                    }

                    if (property.GetCustomAttribute<EncryptedAttribute>() is not null)
                    {
                        continue;
                    }

                    string fullPath = $"{type.FullName}.{property.Name}";
                    if (Exemptions.Contains(fullPath))
                    {
                        continue;
                    }

                    violations.Add($"{fullPath} ([SensitiveData(Level={sensitive.Level})] without [Encrypted])");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every string property classified [SensitiveData(Level >= Confidential)] on a persisted "
            + "type (Entity, Saga, IIntegrationEvent) must also carry [Encrypted] so the value is "
            + "encrypted at rest in the database (EF Core ValueConverter via "
            + "ApplyEncryptionConventions) and on the Wolverine outbox / saga store "
            + "(EncryptedStringJsonConverter via Granit.Wolverine.Encryption). Transient DTOs "
            + "(*Request, *Response) are exempt. Violators: "
            + string.Join("; ", violations));
    }

    private static bool IsPersisted(Type type)
    {
        if (type.IsAbstract && !type.IsSealed)
        {
            // Abstract base classes (e.g. LegalAgreementBase) declare PII for app-defined
            // concrete entities. Their properties are inherited by sealed subclasses; the
            // attribute scan picks them up there.
            return InheritsFrom(type, typeof(Entity)) || InheritsFrom(type, typeof(Saga));
        }

        return InheritsFrom(type, typeof(Entity))
            || InheritsFrom(type, typeof(Saga))
            || typeof(IIntegrationEvent).IsAssignableFrom(type);
    }

    private static bool IsTransientDto(Type type)
    {
        string name = type.Name;
        return name.EndsWith("Request", StringComparison.Ordinal)
            || name.EndsWith("Response", StringComparison.Ordinal)
            || name.EndsWith("Dto", StringComparison.Ordinal);
    }

    private static bool InheritsFrom(Type type, Type candidate)
    {
        Type? current = type.BaseType;
        while (current is not null)
        {
            if (current == candidate)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static Assembly? TryLoadAssembly(string path)
    {
        try
        {
            return Assembly.LoadFrom(path);
        }
        catch
        {
            return null;
        }
    }
}
