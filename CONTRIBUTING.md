# Contributing to Granit

Thank you for your interest in contributing to Granit! This guide will help you get
started.

## Code of Conduct

Please read our [Code of Conduct](CODE_OF_CONDUCT.md) before contributing. We are
committed to providing a welcoming and inclusive experience for everyone.

## Getting Started

### Prerequisites

- **.NET 10 SDK** — `dotnet --version` should return `10.0.x`
- **Git** with SSH access
- **Node.js** (for markdownlint only)

### Build and test

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run tests for a specific package
dotnet test tests/Granit.Users.Tests

# Verify code formatting
dotnet format --verify-no-changes
```

## How to Contribute

### Reporting Bugs

Open an issue using the **Bug** template. Include:

- A clear, concise description of the problem
- Steps to reproduce
- Expected vs actual behavior
- .NET version and OS

### Suggesting Features

Open an issue using the **Feature** template. Describe:

- The use case and motivation
- How it fits into Granit's modular architecture
- Any alternatives you considered

### Submitting Changes

1. **Fork** the repository
2. **Create a branch** from `develop`:

   ```text
   <type>/<short-description>

   Types: feature/ | fix/ | docs/ | refactor/ | chore/ | test/ | perf/
   ```

3. **Write your code** following the conventions below
4. **Write or update tests** — every package has a matching `*.Tests` project
5. **Run the Definition of Done checks** (see below)
6. **Commit** using [Conventional Commits](https://www.conventionalcommits.org/):

   ```bash
   git commit -m "feat(security): add API key rotation support"
   git commit -m "fix(persistence): handle concurrent soft delete"
   git commit -m "docs: update Vault integration guide"
   ```

7. **Open a pull request** against `develop`

### Definition of Done

All checks are **blocking** — a PR will not be merged until they pass:

1. `dotnet test` — zero failures
2. `dotnet format --verify-no-changes` — zero formatting issues
3. `npx markdownlint-cli2 "<file>"` — every modified `.md` file passes
4. Documentation updated if the change affects public API or behavior

## Code Conventions

### C# / .NET

- **Target**: `net10.0`, **C# 14**
- **Nullable**: enabled (`<Nullable>enable</Nullable>`)
- **Warnings as errors**: enabled
- **Central Package Management**: all versions in `Directory.Packages.props`
- **Namespaces**: match the project name (`Granit.{Package}`)
- **`var`**: use when the type is apparent; explicit type otherwise (IDE0008)
- **Expression body** (`=>`): for single-statement methods (IDE0022)
- **Regex**: always `[GeneratedRegex]`, never `new Regex(..., Compiled)`
- **Logging**: always `[LoggerMessage]` source-generated
- **Time**: never `DateTime.Now`/`UtcNow` — inject `TimeProvider` or `IClock`
- **Async**: `ConfigureAwait(false)` in library code, `CancellationToken` as last parameter

### Architecture

- One project = one NuGet package
- Zero circular references between packages
- `*.Abstractions` packages have no dependencies on other Granit packages

### Tests

- **Framework**: xUnit
- **Assertions**: Shouldly
- **Mocking**: NSubstitute
- **Test data**: Bogus

### Security

**Never**:

- Commit secrets or credentials
- Log PII in plain text
- Disable security scans

**Always**:

- Encrypt sensitive data at rest and in transit
- Maintain audit trail for sensitive operations

## Review Process

A maintainer will review your PR against this checklist:

- [ ] No hardcoded secrets
- [ ] Tests pass (`dotnet test`)
- [ ] Build succeeds (`dotnet build`)
- [ ] Format verified (`dotnet format --verify-no-changes`)
- [ ] No PII in logs
- [ ] CHANGELOG.md updated
- [ ] Documentation updated if applicable

## License

By contributing, you agree that your contributions will be licensed under the
[Apache License 2.0](LICENSE).
