---
title: "Contributing to Granit \u2014 Open-Source .NET Framework"
description: Everything you need to contribute to Granit — dev setup, C# coding standards, git workflow, module structure, testing guide, and Definition of Done.
sidebar:
  label: Contributing to Granit
  order: 0
---

Thank you for your interest in contributing to Granit. This section covers
everything you need to get started as a contributor to the framework.

Before contributing, please read the
[Code of Conduct](https://github.com/granit-fx/granit-dotnet/blob/main/CODE_OF_CONDUCT.md)
(Contributor Covenant 2.1). We are committed to providing a welcoming and
inclusive experience for everyone.

## How to contribute

### Reporting bugs

Open an issue using the **Bug** template. Include:

- A clear, concise description of the problem
- Steps to reproduce
- Expected vs actual behavior
- .NET version and OS

### Suggesting features

Open an issue using the **Feature** template. Describe:

- The use case and motivation
- How it fits into Granit's modular architecture
- Any alternatives you considered

### Submitting changes

1. **Fork** the repository
2. **Create a branch** from `develop` (see [Git Workflow](./git-workflow.md))
3. **Write your code** following the [Coding Standards](./coding-standards.md)
4. **Write or update tests** (see [Testing Guide](./testing-guide.md))
5. **Run the [Definition of Done](./definition-of-done.md) checks**
6. **Commit** using Conventional Commits
7. **Open a merge request** against `develop`

## Section contents

- [Development Setup](./development-setup.md) -- prerequisites, build, test, project structure
- [Coding Standards](./coding-standards.md) -- C# style, naming, architecture conventions
- [Module Structure](./module-structure.md) -- how to create a new Granit module
- [Testing Guide](./testing-guide.md) -- xUnit, Shouldly, NSubstitute, Bogus
- [Definition of Done](./definition-of-done.md) -- blocking checklist before any push
- [Git Workflow](./git-workflow.md) -- branches, commits, MR targets, releases

## License

By contributing, you agree that your contributions will be licensed under the
[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0).
