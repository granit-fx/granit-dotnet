# Granit.Testing.IdentityProviders

Provider-agnostic **conformance checks** for Granit identity providers. Every provider
(Keycloak, Entra ID, Cognito, Google Cloud, or a custom one) advertises an
`IIdentityProviderCapabilities` so callers can adapt their behaviour without `try`/`catch`.
Those flags must be internally consistent and must not claim a facet the provider does not
implement — otherwise a caller that trusts the flag hits a runtime failure instead of a clean
no-op.

This package turns that "capabilities are honest" guarantee into a mechanical, non-regressable
invariant: call it from a provider's own test project, which can construct the provider's
internal capabilities and reference its concrete type.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Testing.IdentityProviders
```

## Usage

```csharp
using Granit.Testing.IdentityProviders;

public sealed class KeycloakCapabilityConformanceTests
{
    [Fact]
    public void Capabilities_are_honest() =>
        IdentityProviderCapabilityConformance.AssertConforms(
            new KeycloakIdentityProviderCapabilities(),
            typeof(KeycloakIdentityProvider));
}
```

`AssertConforms` throws `IdentityProviderConformanceException` on the first violation, which the
wrapping `[Fact]` surfaces as a test failure.
