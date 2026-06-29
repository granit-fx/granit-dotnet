# Granit.Vault

Vault abstractions for Granit applications: transit encryption, dynamic database credentials, string encryption interfaces, and arbitrary secret retrieval.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Vault
```

## Abstractions

| Interface | Purpose |
| --------- | ------- |
| `ITransitEncryptionService` | Encrypt/decrypt strings via a transit engine |
| `ITransitMacService` | HMAC-style integrity tags (sign/verify) backed by a vault |
| `IDatabaseCredentialProvider` | Dynamic database credentials with automatic rotation |
| `ISecretStore` | Read arbitrary secrets (mTLS certificates, signing keys, SMTP creds, API keys) |
| `IStringEncryptionProvider` | Column-level string encryption (from `Granit.Encryption`) |

## `ISecretStore` — arbitrary secrets

```csharp
public sealed class MqttBridge(ISecretStore secrets)
{
    public async Task<X509Certificate2> LoadClientCertAsync(CancellationToken ct)
    {
        // Throws SecretNotFoundException if absent — bubble it.
        SecretDescriptor descriptor = await secrets.GetSecretAsync(
            SecretRequest.Latest("mqtt/client-cert"), ct);

        // ExpiresOn is the Azure/HashiCorp-provided rotation hint — use it to schedule reloads.
        var cert = X509CertificateLoader.LoadPkcs12(descriptor.AsBytes(), password: null);
        return cert;
    }
}
```

`TryGetSecretAsync` returns `null` **only** when the secret is absent. Access-denied,
transient and configuration failures propagate as exceptions — the store never masks
an infra failure behind a missing-secret null.

### Caching (opt-in)

Caching is disabled by default for security-by-default. Enable with:

```jsonc
// appsettings.json
"Vault": {
  "SecretStore": {
    "CacheSeconds": 60,              // 0 = disabled (default)
    "MaxCachedBinarySizeBytes": 65536 // LOH guard, default 64 KiB
  }
}
```

The cache decorator is only wired when `CacheSeconds > 0`; otherwise FusionCache is a
zero-cost transitive dependency. Recommended TTL: ≤ 300 s to limit exposure after rotation.

This package ships only the abstractions — caching (and every `ISecretStore`
operation) applies once a provider package (`Granit.Vault.HashiCorp`,
`Granit.Vault.Azure`, …) is registered via its own `AddGranit*` extension.

### Retry

`ISecretStore` does **not** retry automatically. Transient failures (429, 503, timeouts,
gRPC `Unavailable`) surface as `SecretVaultTransientException`. Consumers decide their
policy — wrap the call in Polly if needed:

```csharp
catch (SecretVaultTransientException)
{
    // let Polly retry…
}
```

## `ITransitMacService` — vault-backed MAC

Provider-agnostic HMAC primitive. The tag string is opaque — callers MUST NOT parse it.

```csharp
public sealed class WebhookSigner(ITransitMacService mac)
{
    public async Task<string> SignAsync(byte[] body, CancellationToken ct)
    {
        TransitMacResult result = await mac.MacAsync("granit-webhook-mac", body, ct);
        return result.Mac; // store / send verbatim
    }

    public Task<bool> VerifyAsync(byte[] body, string tag, CancellationToken ct) =>
        mac.VerifyAsync("granit-webhook-mac", body, tag, ct);
}
```

`VerifyAsync` accepts tags signed under any key version still inside the provider's
rotation window — versioned providers (HashiCorp, GCP, Azure HSM) use the native
`min_decryption_version` / enabled-version semantics; alias-based providers (AWS KMS)
expose a `CurrentAlias` / `PreviousAlias` pair.

### `SecretBackedMacService` — portable fallback

When the underlying provider has no native HMAC primitive (Azure Key Vault Standard
tier), register the secret-backed fallback instead:

```csharp
services.AddGranitSecretBackedMacService(o =>
{
    o.CurrentSecretName  = "granit/mac/current";
    o.PreviousSecretName = "granit/mac/previous"; // optional — rolling rotation
    o.RefreshInterval    = TimeSpan.FromMinutes(5);
});
```

The 32-byte key is pulled from any registered `ISecretStore` and cached in process
memory (`CryptographicOperations.ZeroMemory` on dispose / refresh). Trust-boundary
trade-off documented inline on the DI extension.

## Providers

Install a provider package to get a concrete implementation:

| Provider | Package |
| -------- | ------- |
| HashiCorp Vault | `Granit.Vault.HashiCorp` |
| Azure Key Vault | `Granit.Vault.Azure` |
| AWS KMS + Secrets Manager | `Granit.Vault.Aws` |
| Google Cloud KMS + Secret Manager | `Granit.Vault.GoogleCloud` |

## Dependencies

- `Granit.Caching` (used by the `ISecretStore` cache decorator; zero-cost when caching is disabled)
- `Granit.Encryption`

## Documentation

See the [full documentation](https://granit-fx.dev).
