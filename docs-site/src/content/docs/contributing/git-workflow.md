---
title: "Git Workflow \u2014 Branching, PRs & Conventional Commits"
description: GitFlow branching strategy, Conventional Commits format, PR targets, and semantic release process for contributing to the Granit open-source .NET framework.
sidebar:
  label: Git Workflow
  order: 6
---

## Branching (GitFlow)

| Branch type | Convention | MR target |
| ----------- | ---------- | --------- |
| `feature/*` | New features | `develop` |
| `fix/*` | Bug fixes | `develop` |
| `hotfix/*` | Urgent production fixes | `main` + `develop` |
| `release/*` | Pre-release stabilization | `main` + `develop` |

Additional branch prefixes: `docs/`, `refactor/`, `chore/`, `test/`, `perf/` --
all target `develop`.

:::caution[Warning]
**Direct push to `main` is forbidden.** All changes go through a merge request
with at least 1 approval.
:::

## MR targets -- strict rules

| Branch type | Default target | Exception |
| ----------- | -------------- | --------- |
| `feature/*` | `develop` | Only if explicitly requested to target `main` |
| `fix/*` | `develop` | Only if explicitly requested to target `main` |
| `hotfix/*` | `main` + `develop` | Always both |
| `release/*` | `main` + `develop` | Always both |

Never target `main` for a `feature/*` or `fix/*` branch unless explicitly
requested. When in doubt, ask before creating the MR.

## Conventional Commits

All commits use [Conventional Commits](https://www.conventionalcommits.org/)
in **English**:

```text
feat(vault): add Transit AES-256 encryption
fix(persistence): handle audit interceptor on detached entities
docs(guide): create coding conventions guide
chore(ci): update GitHub Actions pipeline
refactor(notifications): extract channel dispatcher
test(security): add JWT rotation tests
perf(caching): reduce Redis roundtrips for hybrid cache
```

### Commit types

| Type | When to use |
| ---- | ----------- |
| `feat` | New feature or capability |
| `fix` | Bug fix |
| `docs` | Documentation changes only |
| `chore` | Build, CI, tooling, dependencies |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `test` | Adding or updating tests |
| `perf` | Performance improvement |

### Scope

The scope in parentheses identifies the affected package or area:

- Package name without the `Granit.` prefix: `(vault)`, `(persistence)`,
  `(notifications)`
- Cross-cutting scopes: `(ci)`, `(build)`, `(deps)`
- Omit the scope for changes that span many packages

### Breaking changes

Add `!` after the type/scope and a `BREAKING CHANGE:` footer:

```text
feat(query-engine)!: change filter syntax from bracket to dot notation

BREAKING CHANGE: filter parameters now use `filter.field.operator=value`
instead of `filter[field][operator]=value`. Update all API clients.
```

## Third-party notices

When adding, removing, or upgrading a dependency:

1. Update `THIRD-PARTY-NOTICES.md` with the package name, version, license
   (SPDX), and copyright holder
2. Update the summary table if license counts changed
3. Update the `Last updated` date
4. Flag any non-permissive license (GPL, AGPL, SSPL) for legal review

Never add or update a dependency without modifying `THIRD-PARTY-NOTICES.md`.

## Code review checklist

A maintainer will review your MR against this checklist:

- [ ] No hardcoded secrets
- [ ] Tests pass (`dotnet test`)
- [ ] Build succeeds (`dotnet build`)
- [ ] Format verified (`dotnet format --verify-no-changes`)
- [ ] No PII in logs
- [ ] `THIRD-PARTY-NOTICES.md` updated if dependencies changed
- [ ] Documentation updated if applicable

## Releases

- Semantic tags on `main`: `vMAJOR.MINOR.PATCH`
- `release/*` branches for stabilization before the tag
- 1 approval minimum for merging to `main`

### Version semantics

| Change | Version bump | Example |
| ------ | ------------ | ------- |
| Breaking API change | MAJOR | v2.0.0 |
| New feature, backward compatible | MINOR | v1.3.0 |
| Bug fix, backward compatible | PATCH | v1.3.1 |

## Language rules

| Content | Language |
| ------- | -------- |
| Code (identifiers, XML docs, comments) | English |
| Commits (Conventional Commits) | English |
| Documentation (`docs/**/*.md`) | English |
| GitHub issues (title, description) | French |
| Localization files (`Localization/**/*.json`) | 17 cultures |

Code must never contain French diacritics. French diacritics
(é, è, ê, à, â, ù, û, ô, î, ï, ç, œ) are required in French-language
content (issues).
