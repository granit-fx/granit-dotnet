---
title: "Migration Guide \u2014 Upgrading Granit Versions"
description: Step-by-step upgrade guides for Granit major versions — semantic versioning policy, breaking change log, and migration steps for all %%PACKAGE_COUNT%% packages.
sidebar:
  label: Migration
  order: 0
---

Granit follows [Semantic Versioning 2.0.0](https://semver.org/). All %%PACKAGE_COUNT%% packages
share a single version number and are released together from the monorepo. This
section documents breaking changes and provides upgrade guides between major
versions.

## Versioning strategy

### Single version for all packages

Every Granit NuGet package (`Granit.Core`, `Granit.Persistence`,
`Granit.Notifications.Email.Smtp`, etc.) ships under the same version number.
When you upgrade one package, upgrade them all. This eliminates version matrix
issues and guarantees that cross-package contracts remain consistent.

```xml
<!-- All packages share the same version -->
<PackageReference Include="Granit.Core" Version="1.2.0" />
<PackageReference Include="Granit.Persistence" Version="1.2.0" />
<PackageReference Include="Granit.Notifications" Version="1.2.0" />
```

### SemVer rules

| Version bump | Meaning | Example |
| ------------ | ------- | ------- |
| **Major** (`X.0.0`) | Breaking changes -- public API removals, behavioral changes, required migrations | Removing a method from `IGuidGenerator` |
| **Minor** (`0.X.0`) | New features, backward-compatible additions | Adding `Granit.Notifications.WhatsApp` |
| **Patch** (`0.0.X`) | Bug fixes, performance improvements, documentation corrections | Fixing a race condition in `HybridCache` |

### Pre-release versions

Pre-release packages use the `-preview.N` suffix (e.g., `2.0.0-preview.1`).
These are published to GitHub Packages for early testing. Public API
may change between preview releases without notice.

## Breaking change policy

Breaking changes are introduced only in major version bumps. Each major release
includes:

- A detailed changelog entry listing every breaking change
- Migration instructions with before/after code samples
- The rationale for the change
- A minimum deprecation period of one minor release cycle before removal

### Deprecation workflow

1. The API is marked with `[Obsolete("Use XYZ instead. Will be removed in vN.0.0")]`
2. The deprecation appears in the changelog for the minor release
3. The API is removed in the next major release

### What counts as a breaking change

- Removing or renaming a public type, method, or property
- Changing method signatures (parameter types, return types)
- Changing default behavior that existing consumers rely on
- Removing a `[DependsOn]` dependency that downstream modules expect
- Changing database schema in a way that requires manual migration
- Removing or renaming configuration keys

### What does not count as a breaking change

- Adding new optional parameters with default values
- Adding new public types or methods
- Adding new `[DependsOn]` dependencies
- Performance improvements
- Bug fixes (even if code relied on the buggy behavior)

## Upgrade checklist

When upgrading Granit to a new version:

1. **Read the changelog** for the target version and every version in between
2. **Update all Granit packages** to the same version simultaneously
3. **Run `dotnet build`** and fix any compilation errors
4. **Run `dotnet test`** to catch behavioral regressions
5. **Check EF Core migrations** -- if schema changes are involved, generate and
   review a new migration
6. **Review deprecated API warnings** and plan replacements before the next major
   release

## Section contents

- [Changelog](./changelog/) -- format, conventions, and version history
