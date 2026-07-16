using Granit.Identity;
using Granit.Testing.IdentityProviders.Exceptions;

namespace Granit.Testing.IdentityProviders;

/// <summary>
/// Provider-agnostic conformance checks for an <see cref="IIdentityProviderCapabilities"/>
/// implementation. A provider advertises capabilities so callers can adapt without try/catch;
/// those flags must therefore be internally consistent and must not claim a facet the provider
/// does not actually implement. Call <see cref="AssertConforms"/> from a provider's own test
/// project (which can construct its internal capabilities and reference its provider type) so
/// every provider is held to the same contract — this is what makes the "capabilities are honest"
/// guarantee (Vague 0) a mechanical, non-regressable invariant.
/// </summary>
public static class IdentityProviderCapabilityConformance
{
    /// <summary>
    /// Asserts that <paramref name="capabilities"/> is self-consistent and honest about the facets
    /// <paramref name="providerType"/> implements. Throws <see cref="IdentityProviderConformanceException"/>
    /// on the first violation.
    /// </summary>
    /// <param name="capabilities">The provider's capabilities descriptor.</param>
    /// <param name="providerType">The concrete provider type advertising <paramref name="capabilities"/>.</param>
    public static void AssertConforms(IIdentityProviderCapabilities capabilities, Type providerType)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(providerType);

        Require(
            !string.IsNullOrWhiteSpace(capabilities.ProviderName),
            providerType,
            "ProviderName must be a non-empty display name.");

        Require(
            capabilities.MaxCustomAttributes >= 0,
            providerType,
            "MaxCustomAttributes must not be negative.");

        // The custom-attribute flag and its count must agree.
        if (capabilities.SupportsCustomAttributes)
        {
            Require(
                capabilities.MaxCustomAttributes > 0,
                providerType,
                "SupportsCustomAttributes is true but MaxCustomAttributes is not positive.");
        }
        else
        {
            Require(
                capabilities.MaxCustomAttributes == 0,
                providerType,
                "SupportsCustomAttributes is false but MaxCustomAttributes is non-zero.");
        }

        // Write support presupposes read support.
        if (capabilities.SupportsClientRoleWrites)
        {
            Require(
                capabilities.SupportsClientRoles,
                providerType,
                "SupportsClientRoleWrites is true but SupportsClientRoles is false.");
        }

        // Interface honesty: claiming client-role support requires actually implementing the facet,
        // otherwise a caller that trusts the flag hits a runtime cast failure instead of a clean no-op.
        if (capabilities.SupportsClientRoles)
        {
            Require(
                typeof(IIdentityClientRoleManager).IsAssignableFrom(providerType),
                providerType,
                $"SupportsClientRoles is true but {providerType.Name} does not implement IIdentityClientRoleManager.");
        }
    }

    private static void Require(bool condition, Type providerType, string message)
    {
        if (!condition)
        {
            throw new IdentityProviderConformanceException($"{providerType.Name}: {message}");
        }
    }
}
