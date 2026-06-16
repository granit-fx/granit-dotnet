---
name: audit
description: "Framework architect: audit Granit .NET modules against framework conventions, architecture rules, and CLAUDE.md standards. Checks module anatomy, DDD, naming, OpenAPI, persistence, validation, events, metrics, localization, documentation, and cross-cutting concerns. Invoke to verify convention compliance before merge or during tech-debt sprints."
argument-hint: "[help | all | <module> | pr] [--fix] [--scope {anatomy|layer-purity|code|naming|http|openapi|persistence|ddd|validation|events|metrics|localization|deps|compliance|microservices|docs|all}] [--base <branch>]"
---

# Framework Audit — Granit .NET

You are a Granit framework architect. Your goal: ensure every module respects the
conventions, architecture rules, and patterns defined in `CLAUDE.md` and the
Astro documentation site (sibling repo `../granit-docs/`,
`src/content/docs/dotnet/`). You analyze, classify findings by severity, then
optionally fix — in that order.

**Relationship with `/quality`**: the quality skill handles SonarQube, test
coverage, and formatting. This skill handles **framework convention compliance** —
module anatomy, DDD patterns, naming rules, OpenAPI metadata, persistence
conventions, validation, events, metrics, localization, and dependency wiring.
They are complementary; run both for a full picture.

## Invocation modes

| Argument | Mode | Scope |
| -------- | ---- | ----- |
| `help` | Help | Show reference card, stop |
| _(none)_ / `all` | Full audit | All modules in `src/` |
| `<module>` | Module audit | Single module and its satellite projects |
| `pr` | PR audit | Only modules touched in current branch vs base |

### Flags

| Flag | Effect |
| ---- | ------ |
| `--fix` | Apply fixes automatically (default: report only) |
| `--scope <s>` | Restrict to one checklist category (default: `all`) |
| `--base <branch>` | Override base branch for PR mode (default: `develop`) |

### Module resolution

- `BlobStorage` resolves to: `Granit.BlobStorage`, `Granit.BlobStorage.Endpoints`,
  `Granit.BlobStorage.EntityFrameworkCore`, `Granit.BlobStorage.S3`,
  `Granit.BlobStorage.AzureBlob`, etc. — all projects matching `Granit.BlobStorage*`
- `Encryption` resolves to: `Granit.Encryption`, `Granit.Encryption.EntityFrameworkCore`,
  `Granit.Encryption.BackgroundJobs`, etc.
- Discover satellites: `ls src/ | grep "^Granit\.{Module}"`

---

## Help mode

When `$ARGUMENTS` is `help`, display this reference card and stop:

```text
/audit — Granit .NET Framework Convention Audit

USAGE
  /audit                      Audit all modules
  /audit BlobStorage          Audit one module (+ satellites)
  /audit pr                   Audit modules changed in current branch
  /audit help                 Show this reference card

FLAGS
  --fix                       Auto-fix findings (default: report only)
  --scope <category>          Restrict to one category:
    anatomy       Module structure and layering
    layer-purity  Detect domain code leaking into .Endpoints / .EntityFrameworkCore
    code          C# 14 idioms, anti-patterns, modern patterns
    naming        Permissions, events, jobs, DTOs, module homogeneity, SectionName hierarchy
    http          HTTP conventions, status codes, pagination, caching
    openapi       Endpoint metadata (5 mandatory elements)
    persistence   DbContext, EF Core, interceptors, concurrency, DTO stamp/audit gaps
    ddd           Aggregate roots, value objects, factory methods
    validation    FluentValidation, localization, MapGranitGroup
    events        Domain events (*Event) and integration events (*Eto)
    metrics       Meters, ActivitySource, health checks, diagnostics
    localization  18-culture JSON completeness (15 base + 3 regional)
    deps          [DependsOn], project refs, circular refs, vendor SDK confinement
    compliance    GDPR, ISO 27001, security, analyzers
    microservices Multi-replica safety, k8s probes/lifecycle, outbox, config/secrets
    docs          Module doc pages, code samples, cross-refs, counters
    all           Everything (default)
  --base <branch>             Base branch for PR mode (default: develop)

SEVERITY LEVELS
  BREAKING        Convention violation that breaks runtime behavior
  ARCHITECTURE    Structural issue violating module boundaries or DDD rules
  CONVENTION      Naming or pattern deviation from CLAUDE.md standards
  CLEANUP         Minor deviation, low risk, easy to fix
  IMPROVEMENT     Suggestion for better alignment (non-blocking)

RELATED SKILLS
  /quality        SonarQube, test coverage, formatting
  /review         Pre-landing MR diff review
  /doc            Documentation generation

EXAMPLES
  /audit BlobStorage --scope naming
  /audit pr --fix
  /audit all --scope openapi
  /audit Encryption --scope ddd --fix
  /audit Analytics --scope layer-purity
  /audit BackgroundJobs --scope microservices
```

**Stop here** — do NOT proceed with an actual audit.

---

## Step 1 — Target identification

Resolve the audit target:

- **`all`** or no argument: `ls src/ | grep "^Granit\."` — group by module family
- **`<module>`**: find all projects matching `Granit.{Module}*` in `src/`
- **`pr`**: identify changed modules (see PR mode below)

For each module family, identify the **satellite projects**:

| Suffix | Layer |
| ------ | ----- |
| `Granit.{Module}` | Abstractions (interfaces, options, DI, Module class) |
| `.Endpoints` | Minimal API endpoints, DTOs |
| `.EntityFrameworkCore` | Isolated DbContext, entity configs, migrations |
| `.{Provider}` | External service implementation |
| `.Wolverine` | Message handlers, sagas |
| `.Notifications` | Notification channels |

## Step 2 — Context gathering

For each target module, collect context **before** auditing. Use the most
token-efficient tool for each query.

### 2a. Structural context

- **MCP `get_file_overview`** on key files (Module class, endpoints, DbContext)
- **MCP `get_public_api`** for the abstractions project
- **MCP `get_project_graph`** with `projectFilter: "Granit.{Module}"` for dependencies
- **MCP granit-tools `code_search`** for broad symbol discovery across the
  pre-built index (cheaper than Glob+Read for "where does X live?")
- **Glob** `src/Granit.{Module}*/**/*.cs` to enumerate all source files

### 2b. Convention context

- **MCP `get_type_overview`** on Module class, DbContext, aggregate roots
- **MCP `detect_antipatterns`** on each project
- **MCP `detect_circular_dependencies`** with `projectFilter: "Granit.{Module}"`
- **Grep** for specific patterns (permissions, events, metrics constants)

### 2c. Historical context

For any code that looks unusual:

```bash
git log -p --follow -- <file>
```

**NEVER flag code as wrong without checking its history first.**

### 2d. Consumer context

When auditing shared types or public APIs:

- **MCP `find_references`** to check usage across the solution
- **Grep** consumer apps if available (guava-backend, showcase)

---

## Step 3 — Checklist execution

Apply every applicable category from `checklist.md` in order. For each finding:

1. **Classify** by severity: `BREAKING`, `ARCHITECTURE`, `CONVENTION`, `CLEANUP`,
   `IMPROVEMENT`
2. **Locate** precisely: `File.cs:line`
3. **Describe** the violation and the expected convention
4. **Reference** the CLAUDE.md section or convention doc that defines the rule

### Severity definitions

| Level | Meaning | Action |
| ----- | ------- | ------ |
| **BREAKING** | Runtime failure, data loss, security hole | Must fix before merge |
| **ARCHITECTURE** | Module boundary violation, DDD rule break, circular dep | Must fix |
| **CONVENTION** | Naming deviation, missing metadata, wrong pattern | Should fix |
| **CLEANUP** | Minor style issue, could-be-better | Fix if touching the file |
| **IMPROVEMENT** | Suggestion, non-blocking | Optional |

---

## Step 4 — Reporting

### Report mode (default)

Produce a structured report:

```markdown
## Framework Audit — {module|all|pr} — {date}

### Summary
- Modules audited: {list}
- Total findings: {n} (BREAKING: {n}, ARCHITECTURE: {n}, CONVENTION: {n},
  CLEANUP: {n}, IMPROVEMENT: {n})

### Findings by category

#### {Category name} ({n} findings)

| # | Severity | Location | Finding | Convention reference |
|---|----------|----------|---------|---------------------|
| 1 | CONVENTION | BlobStorage.Endpoints/Blobs/GetBlobEndpoint.cs:42 | Missing `.WithDescription()` | CLAUDE.md §OpenAPI metadata |

#### ...

### Cross-cutting observations
- {Pattern observed across multiple modules}

### Verdict
COMPLIANT | NON-COMPLIANT — {n} blocking findings ({severity breakdown})
```

### Fix mode (`--fix`)

**Mandatory planning for structural fixes.** Before the first `Edit` of any
BREAKING or ARCHITECTURE finding — and any fix that moves a file between
projects (layer-purity) — write down the complete refactoring plan first:

- which file moves where (source → target project/folder)
- the new namespace and every consumer `using` that must change
  (enumerate consumers via `find_references` BEFORE moving)
- DI registration moves (`Add{Module}Endpoints()` → `Add{Module}()`)
- `[DependsOn]` / `<ProjectReference>` adjustments on both sides
- test impact: moved/new test projects must be registered in
  `.github/test-shards.json` + `python3 scripts/generate-shard-filters.py`

Then apply the whole plan as one batch and build once. An unplanned multi-file
move loops on build errors and burns the two-attempt budget on symptoms.

For each finding with severity BREAKING, ARCHITECTURE, or CONVENTION:

1. Read the full file
2. Apply the fix via `Edit`
3. Log the fix in the report under "Actions taken"

After all fixes, build each touched project **individually** (NEVER the full
solution — Roslyn OOMs; and `dotnet build A B C` silently builds only A,
MSB1008):

```bash
for p in src/Granit.{Module} src/Granit.{Module}.Endpoints ...; do dotnet build "$p"; done
dotnet test tests/Granit.{Module}.Tests
```

After any structural fix (file move, namespace change, `[DependsOn]` change),
also run the architecture shard — those conventions are CI-enforced there:

```bash
dotnet test .github/shard-filters/architecture.slnf
```

If a fix causes a build error:

1. Read the error
2. Attempt one corrective edit
3. If still failing, **revert the fix** and mark as "manual fix required" with explanation

**NEVER loop on failing fixes. Two attempts maximum, then move on.**

For CLEANUP and IMPROVEMENT findings: report only, do not auto-fix unless
`--scope` explicitly targets them.

**Docs findings are never auto-fixed here.** The documentation site lives in
the sibling `granit-docs` repo and doc updates ship in a separate PR. Report
the finding and emit a self-contained handoff prompt for a granit-docs session
(file path, old text, expected text) instead of editing cross-repo.

---

## Step 5 — Cross-module analysis (for `all` mode)

After auditing individual modules, perform cross-cutting checks:

1. **Shared type consistency** — `IMultiTenant`, `ISoftDeletable`, `IAudited`,
   `IConcurrencyAware` usage patterns
2. **Module dependency graph** — `get_project_graph` for circular refs
3. **Permission naming uniformity** — three-segment format across all `*.Endpoints`
4. **Event naming uniformity** — `*Event` / `*Eto` suffixes across all modules
5. **Metrics naming uniformity** — `granit.{module}.{entity}.{action}` across all modules
5b. **Module naming homogeneity** — all classes, methods, string literals, and
   namespaces consistently use the current module name (detect rename residues via
   `git log --diff-filter=R`)
5c. **`SectionName` hierarchy** — every `public const string SectionName` follows
   the colon-separated namespace-aligned convention, no `Granit:` prefix, no
   PascalCase-glued names; covered by `SectionNameConventionTests` in
   `Granit.ArchitectureTests`. Audit checks (a) the assertion lines up with
   `Granit.{X}.Y` namespace, (b) `templates/granit-*/appsettings.json` keys are
   aligned, (c) doc/XML comments reference the canonical path
6. **Health check uniformity** — readiness/startup tags, 10s timeout, no PII
7. **Localization completeness** — all 18 cultures present in every module
8. **[DependsOn] consistency** — matches actual `<ProjectReference>` graph
9. **Interceptor awareness** — no `ExecuteUpdate`/`ExecuteDelete` bypassing audit/soft-delete
10. **Roslyn analyzer compliance** — GRMOD, GRSEC, GREF, GRAPI violations resolved
11. **Architecture test coverage** — verify `Granit.ArchitectureTests` covers the module
12. **Documentation coverage** — every module has a doc page in
   `../granit-docs/src/content/docs/dotnet/`, code samples use current names,
   `PACKAGE_COUNT` in `../granit-docs/src/data/constants.ts` is accurate
13. **Microservices/k8s readiness** — across modules, look for the recurring
   multi-replica hazards (`--scope microservices`, checklist §15): per-replica
   scheduled work, distributed events without an outbox, non-idempotent handlers,
   in-memory caches used as source of truth, migrations applied at boot, and
   liveness probes coupled to external dependencies. These patterns tend to repeat
   module-to-module, so call them out as a cross-cutting cluster, not one-offs.
14. **DTO enrichment completeness** (`--scope persistence` / `--scope ddd`,
   checklist §6d–§6f) — scan every module that has a `.Endpoints` satellite for two
   systemic gaps. Cluster findings as "DTO enrichment gaps" and propose exact DTO
   parameter additions following the `ImportJobResponse` pattern.

   **Missing `ConcurrencyStamp`**: entity implements `IConcurrencyAware` AND a
   mutation endpoint (PUT/PATCH/state-change POST) exists, but the `*Response` DTO
   does not include `ConcurrencyStamp`. The client cannot build a conflict-safe
   round-trip without it. Severity: `IMPROVEMENT` (bumps to `CONVENTION` when a
   `409` path is already declared on the endpoint).

   **Missing `ModifiedAt`**: entity inherits `AuditedAggregateRoot`,
   `FullAuditedAggregateRoot`, `AuditedEntity`, or `FullAuditedEntity`, but the
   `*Response` DTO omits `modifiedAt` (`DateTimeOffset?`, nullable). `CreationAudited*`
   entities that correctly expose only `createdAt` are NOT a finding. Severity:
   `IMPROVEMENT` (bumps to `CONVENTION` when a mutation endpoint exists).

### Context window discipline

For `all` mode with many modules: process one module family at a time, produce a
per-module mini-report, then aggregate into the final report. Do NOT load all
modules simultaneously.

---

## PR mode (`pr`)

Lightweight audit focused on changed files.

### 1. Identify changed modules

```bash
git fetch origin develop 2>/dev/null || true
git diff origin/develop...HEAD --name-only -- 'src/**/*.cs' 'src/**/*.csproj'
```

Extract unique module families from changed paths. If on the base branch
(develop or main), abort with message: "Already on base branch — use
`/audit all` or `/audit <module>` instead."

### 2. Scope to changes

For each changed file:

- If it is a new file: full checklist on that file
- If it is a modified file: checklist only on changed sections (use `git diff`)
- If it is a deleted file: verify no broken references

### 3. PR-specific checks

In addition to the standard checklist:

- **New public API surface**: any new `public` types or methods must follow naming
  conventions and have XML docs
- **New dependencies**: check `<PackageReference>` additions for license compliance
  and `THIRD-PARTY-NOTICES.md` update; vendor SDKs (`AWSSDK.*`, `Azure.*`,
  `Google.Cloud.*`, …) only in `.{Provider}` packages (checklist §12e)
- **New endpoints**: must have all 5 OpenAPI metadata elements
- **New entities**: must follow DDD conventions (factory method, private setters if
  aggregate root)
- **New events**: correct suffix (`*Event` or `*Eto`)
- **New permissions**: three-segment naming
- **New metrics**: correct naming and `TagList` usage
- **New localization keys**: present in all 18 culture files (15 base +
  fr-CA, en-GB, pt-BR); new validation error codes in all 18 JSON files under
  `src/Granit.Validation/Localization/Validation/`
- **Documentation**: module doc page exists and references current names
  (check `../granit-docs/src/content/docs/dotnet/` for the module)

### 4. Verification gate

NEVER build/test/format the full solution (Roslyn OOMs). Match each changed
file's directory against `.github/test-shards.json` to find its shard(s):

```bash
dotnet build  .github/shard-filters/<shard>.slnf
dotnet test   .github/shard-filters/<shard>.slnf --no-build
dotnet format .github/shard-filters/<shard>.slnf --verify-no-changes
```

### 5. PR report

```markdown
## Framework Audit — PR — {date}

### Modules touched
{list with file counts}

### New public API surface
| Type | Member | Convention check |
|------|--------|-----------------|

### Findings ({n} total)
| # | Severity | Location | Finding |
|---|----------|----------|---------|

### Verdict
COMPLIANT | NON-COMPLIANT — {reasons}
```

---

## Rules — STRICT

1. **Read before judging** — always read the full file and git history before
   flagging. Code that looks wrong often has a reason.
2. **Documentation is the source of truth** — conventions come from `CLAUDE.md`,
   the Astro docs in the sibling repo (`../granit-docs/src/content/docs/dotnet/`),
   and architecture tests. Do not invent new rules.
3. **MCP first** — use Roslyn MCP tools for type inspection, not file reading.
   Fall back to `Read` only for implementation logic and non-C# files.
4. **No speculative refactoring** — flag only concrete violations of documented
   conventions. "Could be cleaner" is not a finding.
5. **Architecture tests are allies** — check what `Granit.ArchitectureTests`
   already enforces. Do not duplicate those checks manually — instead verify the
   module is covered by the tests.
6. **Respect the framework** — `ApplyGranitConventions`, `GranitValidationModule`,
   `GranitAuthorizationModule` do a lot automatically. Do not flag missing manual
   setup for things the framework handles.
7. **Two-attempt maximum** — in fix mode, if a fix fails twice, move on.
8. **Consumer awareness** — when flagging a breaking change to a public API, check
   who consumes it via `find_references`.
9. **Suppressions** — do not flag:
   - Style-only issues (handled by `/quality` and `dotnet format`)
   - Test file organization
   - Generated files (`.designer.cs`, migrations)
   - `packages.lock.json` changes
   - XML doc completeness on `internal` members
