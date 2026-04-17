# Changelog

Tous les changements notables de ce projet seront documentés dans ce fichier.

Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/),
et ce projet adhère au [Semantic Versioning](https://semver.org/lang/fr/).

## [Unreleased]

### Security

- `Granit.Vault.{HashiCorp,Azure,Aws,GoogleCloud}` — dynamic database credentials (`IDatabaseCredentialProvider.Username` / `Password`) are now stored in `byte[]` buffers and zeroized via `CryptographicOperations.ZeroMemory` on every rotation, mitigating credential residency in process memory (CWE-522 / GDPR Art. 32). Shared helper: `Granit.Vault.Internal.ZeroizingCredentialStore`.
- `Granit.Vault.HashiCorp` — `HashiCorpSecretStore` now gates the decoded size of KV v2 `__binary` payloads before allocation, protecting the host from OOM triggered by a misconfigured or compromised vault (CWE-400 / CWE-770). New option `Vault:SecretStore:MaxBinaryPayloadBytes` (default 16 MiB).
- `Granit.Encryption` — `AesStringEncryptionProvider` raises PBKDF2-SHA256 iterations from 100 000 to 600 000 to align with OWASP 2023. **Breaking for existing encrypted data:** payloads encrypted with the previous iteration count can no longer be decrypted. No production deployments are affected at this pre-1.0 stage; re-encrypt any persisted ciphertext before upgrading.

### Added

- Initialisation du repository granit-dotnet
- Structure solution .NET 10 avec Central Package Management
- Projets : Abstractions, Security, Persistence, Vault, Observability
- Projets de tests associés
- CLAUDE.md, CONTRIBUTING.md, CI/CD pipeline
- `Granit.Caching` — abstraction `ICacheService<T>` + fournisseur Memory, protection stampede (double-check locking + SemaphoreSlim dans IMemoryCache), chiffrement AES-256-CBC opt-in par type via `[CacheEncrypted]`
- `Granit.Caching.StackExchangeRedis` — fournisseur Redis (StackExchange.Redis), activation `AesCacheValueEncryptor` automatique si `EncryptValues = true`
- `Granit.Caching.Hybrid` — fournisseur HybridCache L1+L2 pour Kubernetes multi-pods, `LocalCacheExpiration ≤ 60 s` pour borner la fenêtre de données obsolètes
- `Granit.Encryption` — `IStringEncryptionService` avec provider AES-256-CBC (`AesStringEncryptionProvider`) ; extensible via Vault Transit Engine
- `Granit.Localization` — localisation JSON modulaire par assembly (`EmbeddedResource`), héritage inter-modules (`[InheritResource]`), culture fallback natif, compatible `IStringLocalizer<T>`
- `Granit.Settings` — paramètres dynamiques avec résolution en cascade `User (U) → Tenant (T) → Global (G) → Configuration (C) → Default (D)`, cache intégré, `ISettingManager` / `ISettingProvider`, `InMemorySettingStore`
- `Granit.Vault` — `ISecretStore` : lecture unifiée de secrets arbitraires (certificats mTLS, clés de signature, credentials SMTP, API keys) avec `SecretDescriptor` (payload texte XOR binaire, versioning, `ExpiresOn`, `ContentType`, tags). Implémentations : HashiCorp KV v2, Azure Key Vault, AWS Secrets Manager, GCP Secret Manager. Cache L1 FusionCache **opt-in** (`Vault:SecretStore:CacheSeconds = 0` par défaut, sécurité-par-défaut). Hiérarchie d'exceptions dédiée (`SecretNotFoundException`, `SecretAccessDeniedException`, `SecretVaultTransientException`, `SecretVaultConfigurationException`) permettant au consommateur d'appliquer sa propre politique de retry. Métriques `granit.vault.secret.read` / `cache_hit` émises uniquement au niveau décorateur (pas de double-comptage). `TryGetSecretAsync` via default interface method : `null` uniquement sur not-found ; 403 / 5xx / config bullent pour éviter de masquer des pannes infra.

### Changed

- `Granit.Users` — module abstrait (interface `ICurrentUserService` uniquement) ; implémentation JWT Bearer déplacée dans `Granit.Authentication.JwtBearer`
- `Granit.Authentication.JwtBearer` — nouveau package : JWT Bearer générique (section `"Authentication"`), `CurrentUserService`, policy `Authenticated`
- `Granit.Authentication.JwtBearer.Keycloak` — nouveau package : extras Keycloak (`KeycloakClaimsTransformation`, `PostConfigure` JWT, policy `Admin`)
- `Granit.MultiTenancy` — découplé de `` (module autonome) ; `AuditedEntityInterceptor` injecte `TenantId` sur les entités `IMultiTenant`
- `Granit.Persistence` — hiérarchie de domaine : `Entity` → `CreationAuditedEntity` → `AuditedEntity` ; `ModelBuilderExtensions` applique les filtres multi-tenant et soft-delete
- `Granit.Vault` — `VaultClientFactory` utilise `IStringLocalizer<VaultLocalizationResource>` pour les messages d'erreur localisés (FR/EN)

---

## Procédure de mise à jour

### Quand mettre à jour ?

- **Toujours** lors d'une MR vers `main`
- **Jamais** lors d'un commit sur une branche feature

### Comment ?

1. Ajouter entrée dans `[Unreleased]` lors de la MR
2. Créer release lors du merge vers `main` (déplacer [Unreleased] vers nouvelle version)
3. Taguer la release : `git tag -a v0.1.0 -m "Release 0.1.0" && git push origin v0.1.0`

### Format des entrées

```markdown
### Added

- Courte description du changement (#issue ou !MR)

### Fixed

- Bug: description précise du bug corrigé (#issue)
```

### Catégories

- **Added** : Nouvelles fonctionnalités
- **Changed** : Changements dans les fonctionnalités existantes
- **Deprecated** : Fonctionnalités bientôt supprimées
- **Removed** : Fonctionnalités supprimées
- **Fixed** : Corrections de bugs
- **Security** : Corrections de vulnérabilités
