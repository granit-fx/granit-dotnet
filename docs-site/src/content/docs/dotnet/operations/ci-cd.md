---
title: CI/CD
description: GitHub Actions CI/CD pipeline for building, testing, and publishing Granit packages
sidebar:
  order: 4
---

This guide covers the CI/CD pipeline for the Granit framework: compilation,
quality gates, security scanning, static analysis, NuGet packaging, and
publication to GitHub Packages and nuget.org.

## Pipeline overview

The pipeline runs on every pull request, push to `develop`/`main`, and
release tag (`vX.Y.Z`). Concurrent runs on the same ref are cancelled
automatically.

| Phase | Jobs | Blocking |
| --- | --- | --- |
| 1. build | `build` | Yes |
| 2. quality | `format`, `test`, `architecture-test`, `integration-test` | `format`, `test`, `architecture-test`: yes. `integration-test`: no |
| 3. security | `secret-detection`, `trivy`, `codeql` | `secret-detection` and `trivy`: yes |
| 4. analysis | `sonarcloud`, `audit` | No (advisory) |
| 5. pack | `pack` | Yes (develop/main/tags only) |
| 6. publish | `publish-github`, `publish-nuget` | Yes |
| 7. docs | `docs` | No (main only) |

## Build commands

These are the core commands used in CI and available for local development:

```bash
# Compile the solution
dotnet build

# Run all unit tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Verify code formatting (fails if changes needed)
dotnet format --verify-no-changes

# Create NuGet packages
dotnet pack -c Release -o ./nupkgs
```

## Job details

### build

Compiles the full solution on `ubuntu-latest` with .NET 10. Integration tests
are excluded from compilation at this stage (`-p:SkipIntegrationTests=true`) to
avoid requiring Docker. Build artifacts (`bin/` and `obj/`) are uploaded for
downstream jobs.

### format

Runs `dotnet format --verify-no-changes` against the build output. This is a
hard gate: pull requests with formatting violations are blocked.

### test

Runs unit tests with OpenCover and Cobertura coverage output. Architecture
tests and integration tests are excluded (`-p:SkipArchitectureTests=true
-p:SkipIntegrationTests=true`). Coverage reports are uploaded as artifacts for
SonarCloud consumption. The job has a 15-minute timeout.

### architecture-test

Runs the `Granit.ArchitectureTests` project separately to validate cross-cutting
architecture rules (NetArchTest). The job has a 10-minute timeout.

### integration-test

Uses a PostgreSQL 18 service container to validate tenant isolation (ISO 27001
requirement). Each `*.Tests.Integration` project is run sequentially. Marked
`continue-on-error` because it depends on service container availability.

### security

Three security jobs run in parallel, independently of the build:

| Job | Tool | Purpose |
| --- | --- | --- |
| `secret-detection` | [Gitleaks](https://github.com/gitleaks/gitleaks) | Detects committed secrets (API keys, passwords) |
| `trivy` | [Trivy](https://github.com/aquasecurity/trivy) | Filesystem vulnerability scan (HIGH/CRITICAL) |
| `codeql` | [CodeQL](https://codeql.github.com/) | Semantic code analysis for security vulnerabilities |

Gitleaks and Trivy are blocking. CodeQL results appear in the repository
**Security** tab under code scanning alerts.

### sonarcloud

Static analysis with code coverage integration. Receives OpenCover reports from
the `test` job. Requires `SONAR_TOKEN`. Runs only when the pull request
originates from the same repository (not from forks). Marked `continue-on-error`
so pipeline completion is not blocked by SonarCloud availability.

### audit

Runs `dotnet list package --vulnerable --include-transitive` and fails if any
vulnerable package is found. The vulnerability report is saved as a job artifact.
Marked `continue-on-error` (advisory).

### pack

Creates NuGet packages with automatic versioning. Runs only on `develop`,
`main`, or version tags:

| Context | Version format |
| --- | --- |
| Release tag `vX.Y.Z` | `X.Y.Z` (stable release) |
| Branch `develop`/`main` | `0.1.0-dev.<run_number>` (prerelease) |

Packed `.nupkg` files are uploaded as the `nupkgs` artifact.

### publish-github

Pushes `.nupkg` files to **GitHub Packages** on pushes to `develop` or `main`.
Authentication uses the automatic `GITHUB_TOKEN` with `packages: write`
permission. No additional secrets are required.

### publish-nuget

Pushes `.nupkg` files to **nuget.org** on version tags (`vX.Y.Z`). Requires the
`NUGET_API_KEY` secret and uses the `nuget-publish` environment for deployment
protection rules.

### docs

Builds the Astro Starlight documentation site and deploys it to **GitHub Pages**
on pushes to `main`. Uses pnpm with Node.js 22. The deployed URL is available
in the `github-pages` environment.

## NuGet cache strategy

Each job uses the `actions/cache` action to share the NuGet package cache,
keyed by `runner.os` and a hash of `**/*.csproj` + `Directory.Packages.props`.
Restore completes in 5-10 seconds with a warm cache. Build artifacts are shared
via `actions/upload-artifact` / `actions/download-artifact` to avoid redundant
compilation in downstream jobs.

## CI variables

### Required secrets

| Secret | Description | Used by |
| --- | --- | --- |
| `NUGET_API_KEY` | nuget.org API key | `publish-nuget` (tag builds only) |

### Optional secrets

| Secret | Description | Used by |
| --- | --- | --- |
| `SONAR_TOKEN` | SonarCloud authentication token | `sonarcloud` |

### Automatic tokens

`GITHUB_TOKEN` is provided automatically by GitHub Actions and is used for
Gitleaks scanning, GitHub Packages publishing, and GitHub Pages deployment. No
manual configuration is required.

## Consuming Granit packages

Applications that depend on Granit add GitHub Packages as a NuGet source:

```xml
<!-- nuget.config -->
<packageSources>
  <add key="github-granit"
       value="https://nuget.pkg.github.com/granit-fx/index.json" />
</packageSources>
<packageSourceMapping>
  <packageSource key="github-granit">
    <package pattern="Granit.*" />
  </packageSource>
</packageSourceMapping>
```

Authentication uses `GITHUB_TOKEN` in CI environments. For local development,
add credentials in `packageSourceCredentials`:

```xml
<packageSourceCredentials>
  <github-granit>
    <add key="Username" value="YOUR_GITHUB_USERNAME" />
    <add key="ClearTextPassword" value="YOUR_PERSONAL_ACCESS_TOKEN" />
  </github-granit>
</packageSourceCredentials>
```

The personal access token needs the `read:packages` scope.

## Definition of Done

Before any pull request is approved, the following gates must pass:

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes with adequate coverage
- [ ] `dotnet format --verify-no-changes` passes
- [ ] Architecture tests pass
- [ ] No HIGH/CRITICAL vulnerabilities (Trivy)
- [ ] Secret detection scan clean (Gitleaks)
- [ ] Documentation updated (if applicable)

:::caution[Blocking gates]
Tests passing, format clean, and documentation updated are **blocking** requirements.
Do not merge without satisfying all gates. See the full
[Definition of Done](/contributing/definition-of-done/) for details.
:::

## Troubleshooting

### Test job times out

The `test` job has a 15-minute timeout. If tests are slow, check for
non-parallelized test collections. You can adjust the max CPU count in the
workflow:

```yaml
-- RunConfiguration.MaxCpuCount=4
```

### SonarCloud shows 0% coverage

Verify that:

1. The `test` job produces `**/coverage.opencover.xml` artifacts.
2. The `sonarcloud` job downloads the `coverage` artifact.
3. `sonar.cs.opencover.reportsPaths` points to `**/coverage.opencover.xml`.

### audit job fails

A NuGet dependency has a known vulnerability. Check the `vulnerability-report`
artifact for details. Update the affected package or, if a fix is not available,
document the risk assessment and mark the advisory as accepted.

### Integration tests fail with connection errors

Verify that:

1. The PostgreSQL service container is healthy (check the job logs for health
   check output).
2. Environment variables `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`,
   `POSTGRES_USER`, and `POSTGRES_PASSWORD` match the service definition.
3. The test fixture connection string uses `localhost:5432` (service containers
   are mapped to the runner's localhost).

### CodeQL analysis is slow

CodeQL performs a full build for semantic analysis. If the job exceeds the
default timeout, ensure `SkipIntegrationTests=true` is set to reduce build
scope. CodeQL results appear in the repository **Security > Code scanning
alerts** tab.

### publish-github fails with 403

Ensure the workflow has `packages: write` permission. This is declared in the
`publish-github` job definition. For organization repositories, verify that
GitHub Actions has permission to create packages in the organization settings.
