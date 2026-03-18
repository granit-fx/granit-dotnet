---
title: Definition of Done for .NET Contributors
description: Blocking Definition of Done checklist for Granit — tests, dotnet format, markdownlint, docs, and security checks that must all pass before any push or PR.
sidebar:
  label: Definition of Done
  order: 5
---

The Definition of Done is **blocking**. No push or merge request creation may
happen until all checks are satisfied.

## Required checks -- backend (.NET)

### 1. Tests

Every modified package must have its `*.Tests` project updated. All tests must
pass with zero failures:

```bash
dotnet test
```

### 2. Documentation

Any change to a public API, new feature, or behavior modification must be
reflected in the documentation.

### 3. Format

Code formatting must pass with zero issues:

```bash
dotnet format --verify-no-changes
```

### 4. Markdownlint

Every modified `.md` file must pass markdownlint:

```bash
npx markdownlint-cli2 "path/to/file.md"
```

### 5. Third-party notices

Any dependency added, removed, or updated must be reflected in
`THIRD-PARTY-NOTICES.md`:

- Add/modify/remove the package entry with its name, version, license (SPDX
  identifier), and copyright holder
- Update the summary table at the top if the license count changes
- Update the `Last updated` date

:::caution[Warning]
Report immediately any dependency under a **non-permissive license** (GPL, LGPL,
AGPL, SSPL, or commercial restriction). ISO 27001/commercial context requires
legal review before integration.
:::

## Required checks -- frontend (TypeScript / React)

If you are also modifying frontend code:

1. `pnpm test` -- zero failures
2. `pnpm lint` -- ESLint `--max-warnings 0` passes
3. `pnpm exec tsc --noEmit` -- compiles without error
4. `npx prettier --check "src/**/*.{ts,tsx,css}"` -- passes
5. `npx markdownlint-cli2 "path/to/file.md"` -- passes
6. Every new visible component has a Storybook story
7. `THIRD-PARTY-NOTICES.md` updated if dependencies changed

## Quick reference script

Run all backend checks in sequence:

```bash
dotnet build && \
dotnet test && \
dotnet format --verify-no-changes && \
npx markdownlint-cli2 "docs/**/*.md"
```

## Refusal rule

If a push is requested without the DoD being satisfied, the missing checks
must be identified and resolved first. The DoD can only be overridden by
explicit, per-item acknowledgement.

## Security checks

In addition to the DoD, verify before every push:

- No hardcoded secrets or credentials in the code
- No PII logged in plain text
- Sensitive data encrypted at rest and in transit
- Audit trail maintained for sensitive operations

These are not optional -- they are compliance requirements (ISO 27001, GDPR).
