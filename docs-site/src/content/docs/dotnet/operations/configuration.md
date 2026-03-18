---
title: "Production Configuration \u2014 Vault & Env Vars"
description: Production configuration for Granit apps — HashiCorp Vault secret management, dynamic database credentials, environment variable overrides, appsettings layering, and zero-secret-in-code enforcement.
sidebar:
  order: 2
  label: Configuration
---

This guide covers production configuration for Granit applications: secret
management with HashiCorp Vault, environment variable overrides, appsettings
layering, and dynamic database credentials.

## Configuration layering

Granit follows the standard ASP.NET Core configuration precedence (last wins):

1. `appsettings.json` -- base defaults, committed to source control
2. `appsettings.{Environment}.json` -- environment-specific overrides
3. Environment variables -- container-level overrides (Kubernetes `env` or `ConfigMap`)
4. HashiCorp Vault -- secrets (dynamic credentials, encryption keys, API tokens)

:::caution[No secrets in appsettings]
`appsettings.json` and `appsettings.Production.json` must never contain secrets.
Connection strings, API keys, and passwords come exclusively from Vault or
environment variables injected from Vault. This is enforced by CI secret detection
scans and ISO 27001 audit requirements.
:::

## Vault integration (Granit.Vault)

### Architecture

In production, Granit authenticates to Vault using the pod's Kubernetes
ServiceAccount. No static tokens or passwords are stored anywhere.

The `Granit.Vault` package (built on VaultSharp 1.17+) provides:

- **Kubernetes authentication**: automatic JWT-based login using the ServiceAccount token
- **Dynamic PostgreSQL credentials**: ephemeral database users with short TTLs
- **Transit encryption**: field-level encryption without exposing keys to the application
- **Automatic lease renewal**: the `IVaultCredentialLeaseManager` renews leases before expiration

### Vault configuration

```json
{
  "Vault": {
    "Address": "https://vault.internal:8200",
    "AuthMethod": "Kubernetes",
    "KubernetesRole": "my-backend",
    "KubernetesTokenPath": "/var/run/secrets/kubernetes.io/serviceaccount/token",
    "DatabaseMountPoint": "database",
    "DatabaseRoleName": "readwrite",
    "TransitMountPoint": "transit",
    "LeaseRenewalThreshold": 0.75
  }
}
```

| Property | Description | Default |
| --- | --- | --- |
| `Address` | Vault server URL | (required) |
| `AuthMethod` | `"Kubernetes"` (production) or `"Token"` (dev only) | `"Kubernetes"` |
| `KubernetesRole` | Vault role bound to the pod's ServiceAccount | `"my-backend"` |
| `KubernetesTokenPath` | Path to the mounted ServiceAccount JWT | `/var/run/secrets/kubernetes.io/serviceaccount/token` |
| `DatabaseMountPoint` | Vault Database secrets engine mount point | `"database"` |
| `DatabaseRoleName` | Database role for dynamic credential generation | `"readwrite"` |
| `TransitMountPoint` | Vault Transit secrets engine mount point | `"transit"` |
| `LeaseRenewalThreshold` | Fraction of TTL at which to renew the lease (0.0-1.0) | `0.75` |

### Authentication flow

1. The pod reads its ServiceAccount JWT from the mounted path.
2. Granit sends the JWT to Vault's Kubernetes auth endpoint (`POST /auth/kubernetes/login`).
3. Vault verifies the JWT with the Kubernetes API server (TokenReview).
4. Vault returns a client token (TTL 1h, renewable).
5. `IVaultCredentialLeaseManager` renews the token automatically before expiration.

:::note[Token auth for development]
For local development, set `AuthMethod` to `"Token"` and provide a `Token` value.
This avoids requiring a Kubernetes cluster locally. Never use Token auth in production.
:::

## Dynamic database credentials

Vault generates ephemeral PostgreSQL credentials with short TTLs. The credential
lifecycle is fully automated:

1. **Obtain**: on application startup, Granit requests credentials from Vault's
   Database engine.
2. **Active**: EF Core uses the dynamic username/password for all database operations.
3. **Renew**: when the lease reaches the renewal threshold (default 75% of TTL),
   `IVaultCredentialLeaseManager` renews it transparently.
4. **Revoke**: on pod shutdown, credentials are revoked immediately to minimize
   the exposure window.

There are no static database passwords anywhere in the system.

## Transit encryption (field-level)

The Transit engine encrypts and decrypts data without the application ever seeing
the encryption key:

```json
{
  "Vault": {
    "TransitMountPoint": "transit"
  }
}
```

Use `ITransitEncryptionService` in application code to encrypt sensitive fields
(GDPR personal data, health records). The ciphertext includes the key version
(`vault:v2:...`), so key rotation is transparent -- old data remains readable
after rotation.

### Key rotation

```bash
# Rotate the Transit key (Vault CLI or API)
vault write -f transit/keys/my-key/rotate
```

After rotation, new writes use the latest key version. Existing ciphertexts
are decrypted using the version encoded in their prefix.

## Environment variables

For non-secret configuration, use environment variables in Kubernetes:

```yaml
env:
  - name: ASPNETCORE_ENVIRONMENT
    value: "Production"
  - name: Observability__ServiceName
    value: "my-backend"
  - name: Observability__OtlpEndpoint
    value: "http://otel-collector.monitoring:4317"
```

ASP.NET Core maps `__` (double underscore) to the `:` separator in configuration
keys. This works for all Granit configuration sections (`Observability`, `Vault`,
etc.).

## Connection strings

Connection strings for PostgreSQL and Redis follow the standard
`ConnectionStrings` section pattern:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myapp;",
    "Redis": "localhost:6379"
  }
}
```

In production, the PostgreSQL connection string should **not** include username
and password. Vault dynamic credentials are injected at runtime by the
`IVaultCredentialLeaseManager`, which updates the connection string automatically.

## Multi-environment appsettings

Structure your configuration files for clear environment separation:

| File | Contents | Committed |
| --- | --- | --- |
| `appsettings.json` | Defaults, feature flags, non-sensitive settings | Yes |
| `appsettings.Development.json` | Local dev overrides (localhost URLs, debug logging) | Yes |
| `appsettings.Production.json` | Production-specific non-secret values (log levels, timeouts) | Yes |
| `appsettings.Staging.json` | Staging-specific overrides | Yes |

:::tip[Override hierarchy]
Environment variables always win over appsettings files. Use this to override
individual settings per deployment without changing committed files. Vault
secrets take the highest precedence because they are injected directly into
the configuration pipeline.
:::

## Vault monitoring

Track these metrics to detect credential lifecycle issues:

| Metric | Alert threshold | Description |
| --- | --- | --- |
| Active lease count | > 1000 | Potential lease leak |
| Token renewal failures | > 0 over 5 min | Imminent loss of access |
| Seal status | `sealed = true` | Vault sealed -- manual intervention required |
| Storage backend latency | > 100ms | Raft storage degradation |
