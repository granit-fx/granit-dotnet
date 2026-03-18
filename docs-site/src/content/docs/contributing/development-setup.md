---
title: .NET Development Environment Setup
description: Set up a Granit contributor environment — .NET 10 SDK, Docker, Rider or VS Code, solution build, and integration test prerequisites covered step by step.
sidebar:
  label: Development Setup
  order: 1
---

## Prerequisites

| Tool | Version | Check |
| ---- | ------- | ----- |
| .NET SDK | **10.0.x** | `dotnet --version` |
| Git | 2.40+ with SSH access | `git --version` |
| Node.js | 22+ (for markdownlint) | `node --version` |
| Docker | 24+ (for integration tests) | `docker --version` |

### Recommended IDEs

- **JetBrains Rider** (recommended) -- full support for Roslyn analyzers, `.editorconfig`, and solution-wide analysis
- **Visual Studio 2022** (17.14+) -- ensure the .NET 10 workload is installed
- **VS Code** -- with the C# Dev Kit extension

## Clone and build

```bash
git clone git@github.com:granit-fx/granit-dotnet.git
cd granit-dotnet

# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run tests for a specific package
dotnet test tests/Granit.Security.Tests

# Verify code formatting
dotnet format --verify-no-changes

# Validate markdown files
npx markdownlint-cli2 "docs/**/*.md"
```

## NuGet packaging

```bash
# Pack all packages locally
dotnet pack -c Release -o ./nupkgs
```

All package versions are managed centrally in `Directory.Packages.props` (Central Package Management).

## Project structure overview

```text
granit-dotnet/
├── src/                    Source packages (one project = one NuGet package)
│   ├── Granit.Core/
│   ├── Granit.Timing/
│   ├── Granit.Vault/
│   └── ...
├── tests/                  Test projects (mirror of src/)
│   ├── Granit.Core.Tests/
│   ├── Granit.Timing.Tests/
│   ├── Granit.ArchitectureTests/
│   └── ...
├── docs-site/              Starlight documentation site
├── Directory.Build.props   Shared build properties (nullable, warnings-as-errors)
├── Directory.Packages.props Central Package Management
├── BannedSymbols.txt       Banned API list (enforced at compile time)
└── Granit.sln              Solution file
```

:::note[One project = one NuGet package]
Each project under `src/` produces exactly one NuGet package. The namespace
matches the project name (`Granit.Vault` lives in namespace `Granit.Vault`).
There are zero circular references between packages.
:::

## Build properties

The following settings are enforced globally via `Directory.Build.props`:

- **Nullable reference types**: enabled (`<Nullable>enable</Nullable>`)
- **Warnings as errors**: enabled -- the project must compile with zero warnings
- **Target framework**: `net10.0`
- **Language version**: C# 14
- **ImplicitUsings**: enabled

## Docs site

The documentation site uses [Astro Starlight](https://starlight.astro.build/).
To run it locally:

```bash
cd docs-site
pnpm install
pnpm dev
```

## Next steps

- Read the [Coding Standards](./coding-standards/) before writing code
- Check the [Module Structure](./module-structure/) if you are creating a new package
- Review the [Definition of Done](./definition-of-done/) before pushing
