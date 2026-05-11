# Contributing to Granit

Thank you for your interest in contributing to Granit! This guide will help you get started.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) and [pnpm](https://pnpm.io/) (for the docs site)

### Setup

```bash
git clone https://github.com/granit-fx/granit-dotnet.git
cd granit-dotnet
dotnet restore
dotnet build
dotnet test
```

## Development workflow

1. **Fork** the repository and create a branch from `develop`
2. **Name your branch**: `feature/short-description` or `fix/short-description`
3. **Make your changes** following the conventions below
4. **Run checks** before pushing:

   ```bash
   dotnet build
   dotnet test
   dotnet format --verify-no-changes
   ```

5. **Open a Pull Request** targeting `develop`

## Code conventions

- **C# 14** with modern idioms: primary constructors, collection expressions, pattern matching
- **`var`** when the type is apparent; explicit type otherwise
- **Expression body** (`=>`) for single-statement methods
- **`[GeneratedRegex]`** instead of `new Regex(..., Compiled)`
- **`[LoggerMessage]`** instead of string interpolation in log calls
- **`ConfigureAwait(false)`** in library code
- **No `DateTime.Now`/`UtcNow`** — inject `TimeProvider`

See [conventions documentation](https://granit-fx.dev/contributing/coding-standards/) for the full list.

## Commit messages

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(persistence): add soft-delete interceptor
fix(auth): handle expired token refresh
docs: update BlobStorage guide
chore(ci): update GitHub Actions workflow
```

## Pull request guidelines

- Keep PRs focused on a single change
- Include tests for new functionality
- Update documentation if your change affects the public API
- Ensure CI passes (build, tests, format, markdownlint)
- No hardcoded secrets, tokens, or PII in code or logs

## Project structure

```
src/Granit.{Module}/              # Abstractions + DI registration
src/Granit.{Module}.{Provider}/   # Provider implementations
tests/Granit.{Module}.Tests/      # Unit tests (xUnit + Shouldly + NSubstitute)
```

Each module follows a consistent layered architecture. See the
[architecture documentation](https://granit-fx.dev/architecture/) for details.

## Documentation site

The documentation site lives in the sibling repo
[`granit-fx/granit-docs`](https://github.com/granit-fx/granit-docs) (Astro + Starlight,
published to <https://granit-fx.dev>). Open a separate PR there for doc changes.

## Reporting issues

- **Bugs**: Use the [Bug Report](https://github.com/granit-fx/granit-dotnet/issues/new?template=bug_report.yml) template
- **Features**: Use the [Feature Request](https://github.com/granit-fx/granit-dotnet/issues/new?template=feature_request.yml) template
- **Questions**: Open a [Discussion](https://github.com/granit-fx/granit-dotnet/discussions)
- **Security**: See our [Security Policy](https://github.com/granit-fx/granit-dotnet/security/policy)

## License

By contributing, you agree that your contributions will be licensed under the [Apache 2.0 License](../LICENSE).
