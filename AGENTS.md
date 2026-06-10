# AGENTS.md - Granit Codex guide

This repository already maintains its agent instructions in `CLAUDE.md`.
When working here, read and follow:

1. `~/.claude/CLAUDE.md` for global workflow rules.
2. `./CLAUDE.md` for repository-specific architecture, conventions, commands, and anti-patterns.

Treat those files as the source of truth for project guidance. If a rule in those files conflicts with system, safety, sandbox, or user instructions, follow the higher-priority instruction and call out the conflict when it matters.

Quick reminders for this repo:

- Do not run `dotnet build`, `dotnet test`, or `dotnet format` on the full solution.
- Target a `.slnf` shard or a single project/test project.
- Keep code and docs PRs decoupled; docs live in the sibling `granit-docs` repo.
- Preserve layer purity between base modules, `.Endpoints`, `.EntityFrameworkCore`, provider, Wolverine,
  notifications, and background job packages.
