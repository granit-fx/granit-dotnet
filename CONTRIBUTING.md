# Contributing to Granit

Thank you for your interest in Granit.

## Contribution policy

Granit is currently developed under a **maintainer-only** model. The framework is
pre-1.0, the architecture is still consolidating, and the project is maintained by
a single person — review bandwidth is the bottleneck, not code volume.

Concretely:

- **Code contributions** (pull requests against `develop` / `main`) are **not
  accepted from external contributors at this time.** PRs opened without a prior
  invitation from a maintainer will be closed without merge. This is not a
  judgement on the code; it is a scope decision.
- **Issues, bug reports, security reports, and discussions** are very welcome and
  actively read:
  - Bugs → [Bug Report](https://github.com/granit-fx/granit-dotnet/issues/new?template=bug_report.yml)
  - Features → [Feature Request](https://github.com/granit-fx/granit-dotnet/issues/new?template=feature_request.yml)
  - Questions → [Discussions](https://github.com/granit-fx/granit-dotnet/discussions)
  - Vulnerabilities → [Security Policy](https://github.com/granit-fx/granit-dotnet/security/policy)
- If you would like to contribute code, **open an issue or a discussion first.**
  A maintainer will respond if and when the scope is a good fit for an outside
  contribution and will explicitly invite a PR.
- This policy will be relaxed as the framework stabilizes. The rest of this
  document remains as reference for invited contributors and for the project's
  internal conventions.

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

```text
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
- Do not disable analyzers (`#pragma warning disable`, `<NoWarn>`, `dotnet_diagnostic.*.severity = none`) to make code compile. Fix the underlying issue. PRs that suppress analyzers without a `// justification:` comment will be rejected.
- Do not add Markdown files at the repo root, top-level analysis/solution write-ups, or unsolicited `samples/` projects. Framework documentation lives in [`granit-fx/granit-docs`](https://github.com/granit-fx/granit-docs); open a separate PR there.

## AI-assisted contributions

Using AI tools (Claude Code, Copilot, Cursor, ChatGPT, etc.) to help write code is **welcome**. Submitting un-reviewed AI output is **not**.

If any part of your PR was generated or substantially shaped by an AI assistant:

1. **Disclose it** in the PR description, in a `## AI assistance` section, naming the tool(s) and what was AI-generated (code, tests, commit messages, docs).
2. **You are the author.** You are responsible for every line, every dependency, every analyzer suppression. "The agent did it" is not an acceptable response to review comments.
3. **Strip agent artifacts** before pushing: no `## Overview / ## Problem / ## Solution / ## Design Decisions / ✓ …` boilerplate in commit messages, no `from subagent review` / `from agent feedback` mentions, no auto-generated `ISSUE_XXXX_SOLUTION.md` files.
4. **Match the scope of the linked issue.** If the issue asks for a carve-out, do not ship a webapp sample, a frontend, a 400-line design doc, and three speculative abstractions alongside it. Split into separate, scoped PRs.
5. **Be ready to defend the design.** Reviewers may ask open-ended questions about trade-offs (not multiple-choice). If you cannot answer because the agent made the call, that is a signal you have not internalized the change — please re-read your own diff before responding.

Large unscoped or undisclosed AI-generated PRs will be closed and the author asked to resubmit in scoped pieces with a disclosure. We do this to protect the framework and our review bandwidth, not to discourage AI use.

## Project structure

```text
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
