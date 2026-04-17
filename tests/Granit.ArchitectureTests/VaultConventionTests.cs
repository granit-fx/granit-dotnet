using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Granit.Vault;
using Granit.Vault.Exceptions;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates conventions for the <see cref="ISecretStore"/> abstraction and its
/// provider implementations across Granit.Vault.* packages.
/// </summary>
public sealed class VaultConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    private static readonly IObjectProvider<Class> ConcreteSecretStores =
        Classes().That().ImplementInterface(typeof(ISecretStore))
            .And().AreNot(typeof(ISecretStore))
            .As("ISecretStore implementations");

    [Fact]
    public void ISecretStore_should_be_defined_in_Granit_Vault_assembly()
    {
        typeof(ISecretStore).Assembly.GetName().Name.ShouldBe("Granit.Vault");
        typeof(ISecretStore).Namespace.ShouldBe("Granit.Vault");
        typeof(ISecretStore).IsPublic.ShouldBeTrue();
    }

    [Fact]
    public void SecretStore_implementations_should_be_internal_sealed()
    {
        // Concrete provider stores (HashiCorp/Azure/Aws/GoogleCloud) plus the two internal
        // decorators (CachedSecretStore, ProviderTaggingSecretStore) must all be internal
        // sealed classes — the public surface is the ISecretStore interface only.
        IArchRule rule = Classes().That().Are(ConcreteSecretStores)
            .Should().BeSealed()
            .AndShould().NotBePublic()
            .Because("ISecretStore implementations are internal sealed; the abstraction is the only public type");

        rule.Check(Architecture);
    }

    [Fact]
    public void SecretStore_implementations_should_have_Store_suffix()
    {
        IArchRule rule = Classes().That().Are(ConcreteSecretStores)
            .Should().HaveNameEndingWith("SecretStore")
            .Because("naming convention: *SecretStore");

        rule.Check(Architecture);
    }

    [Fact]
    public void SecretVaultException_should_be_abstract()
    {
        typeof(SecretVaultException).IsAbstract.ShouldBeTrue(
            "the base exception type must be abstract to force concrete subclasses for each failure mode");
    }

    [Fact]
    public void SecretVaultException_subclasses_should_derive_from_allowed_bases()
    {
        // Every Granit.Vault.Exceptions.* type must inherit from SecretVaultException, or from
        // Granit.Exceptions.NotFoundException / ForbiddenException (the two framework-level
        // user-friendly bases used by SecretNotFoundException / SecretAccessDeniedException).
        Type[] exceptionTypes = typeof(SecretVaultException).Assembly.GetTypes()
            .Where(t => t.Namespace == "Granit.Vault.Exceptions" && typeof(Exception).IsAssignableFrom(t))
            .ToArray();

        foreach (Type type in exceptionTypes)
        {
            bool ok = type == typeof(SecretVaultException)
                || typeof(SecretVaultException).IsAssignableFrom(type)
                || typeof(Granit.Exceptions.NotFoundException).IsAssignableFrom(type)
                || typeof(Granit.Exceptions.ForbiddenException).IsAssignableFrom(type)
                || typeof(InvalidOperationException).IsAssignableFrom(type); // RetiredKeyVersionException

            ok.ShouldBeTrue(
                $"{type.FullName} must inherit from SecretVaultException, NotFoundException, ForbiddenException or InvalidOperationException");
        }
    }

    [Fact]
    public void ISecretStore_TryGetSecretAsync_should_not_be_overridden_by_providers()
    {
        // Default interface method on ISecretStore centralises the anti-footgun contract:
        // only SecretNotFoundException → null; every other failure bubbles. Providers must
        // NOT reimplement it, or they risk swallowing 403/503 under the guise of "missing".
        Type[] concrete = typeof(ISecretStore).Assembly.GetTypes()
            .Concat(AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name!.StartsWith("Granit.Vault.", StringComparison.Ordinal))
                .SelectMany(a => a.GetTypes()))
            .Distinct()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ISecretStore).IsAssignableFrom(t))
            .ToArray();

        foreach (Type type in concrete)
        {
            // GetMethod with declared-only BindingFlags finds local redefinitions.
            MethodInfo? tryGet = type.GetMethod(
                nameof(ISecretStore.TryGetSecretAsync),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            // Decorators like CachedSecretStore and ProviderTaggingSecretStore are allowed to
            // forward TryGetSecretAsync because they stack on ISecretStore (the inner call still
            // hits the default interface method). The risk is only concrete PROVIDER stores
            // shadowing it — they must only implement GetSecretAsync.
            bool isDecorator = type.Name.Contains("Cached", StringComparison.Ordinal)
                || type.Name.Contains("Tagging", StringComparison.Ordinal);

            if (!isDecorator)
            {
                tryGet.ShouldBeNull(
                    $"{type.FullName} must not reimplement TryGetSecretAsync — rely on the default interface method.");
            }
        }
    }

    [Fact]
    public void SecretDescriptor_ToString_should_redact_payload()
    {
        // Defence-in-depth: even if a logger sink bypasses [SensitiveData], ToString()
        // must not leak the secret contents.
        var descriptor = SecretDescriptor.FromString(
            "secrets/api-key",
            "super-sensitive-token-value");

        string rendered = descriptor.ToString();

        rendered.ShouldNotContain("super-sensitive-token-value",
            Case.Sensitive,
            "SecretDescriptor.ToString() must redact the payload to protect against logger sinks that ignore [SensitiveData]");
    }

    [Fact]
    public void SecretDescriptor_ToString_should_redact_binary_payload()
    {
        byte[] payload = [0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE];
        var descriptor = SecretDescriptor.FromBinary("secrets/cert", payload);

        string rendered = descriptor.ToString();

        rendered.ShouldNotContain("DEADBEEFCAFE", Case.Insensitive);
        rendered.ShouldContain("redacted", Case.Insensitive);
    }
}
