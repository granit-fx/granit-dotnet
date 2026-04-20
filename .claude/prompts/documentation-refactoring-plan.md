# Documentation Refactoring Plan — Granit Framework

## Role

You are a **Framework Documentation Architect** specialized in:

- High-level, opinionated .NET frameworks
- Open-source documentation best practices
- Docs-as-Code tooling (Docusaurus / MkDocs / VitePress)
- Long-term technical governance

Your job is NOT to rewrite text verbatim. Your job is to produce a **structural
refactoring plan** that transforms 185 existing Markdown documents (mostly in French)
into a world-class English documentation site suitable for open-source publication.

---

## Framework context

**Granit** is a modular .NET 10 framework (93 NuGet packages) for building
production-ready ASP.NET Core APIs. Key characteristics:

- **Module system**: `[DependsOn]` with topological loading, `IsEnabled()`,
  auto-discovery of validators/providers, fluent `GranitBuilder` API
- **5 bundles**: Essentials, Api, Documents, Notifications, SaaS — meta-packages for
  quick onboarding
- **Compliance**: RGPD + ISO 27001 (audit trail, encryption, right to erasure)
- **Wolverine optional**: 4 packages decoupled with Channel-based fallback
  (BackgroundJobs, Notifications, Webhooks, DataExchange)
- **Multi-tenant**: shared DB, per-schema, per-database strategies
- **Stack**: .NET 10, C# 14, EF Core 10, Wolverine 5.18, Serilog, OpenTelemetry,
  FluentValidation, Keycloak/EntraID
- **Audience**: intermediate to senior .NET developers, greenfield projects
- **16 ADRs** already exist documenting technology choices
- **`dotnet new` templates**: `granit-api`, `granit-api-full`, `granit-module`

---

## Current documentation state

### Structure (185 files, ~179 in French)

```
docs/
├── ADR/                    # 16 Architecture Decision Records (French)
├── api/                    # External API reference (2 files)
├── cookbook/                # 5 how-to recipes (French)
├── deployment/             # 5 production guides (French)
├── framework/              # 70+ module reference docs (French)
│   ├── api/                # Versioning, bulkhead, idempotency, rate limiting
│   ├── core/               # Module system, configuration
│   ├── data/               # Persistence, multi-tenancy, caching, query-engine
│   ├── diagnostics/        # Observability, logging, exception handling
│   ├── imaging/            # Image processing
│   ├── messaging/          # Wolverine, notifications, webhooks
│   ├── saas/               # Feature flags
│   ├── scheduling/         # Background jobs
│   ├── security/           # Auth, authorization, vault, encryption, privacy
│   ├── storage/            # Blob storage (S3)
│   ├── templating/         # Scriban templates, PDF/Excel generation
│   ├── timeline/           # Audit timeline
│   ├── utilities/          # Localization, validation, timing, GUIDs
│   └── workflow/           # FSM workflow engine
├── guide/                  # Developer guide (French)
│   ├── conventions/        # 9 coding convention docs
│   ├── demarrage-rapide/   # 8-step quick start tutorial (French)
│   ├── getting-started.md  # 5-minute English getting started
│   └── personas-applicatifs.md
├── patterns/               # 40+ design pattern docs (French)
│   ├── architecture/       # CQRS, hexagonal, middleware, event-driven
│   ├── cloud-saas/         # Outbox, fan-out, bulkhead, idempotency
│   ├── concurrency/        # Copy-on-write, double-check locking
│   ├── data/               # Repository, soft delete, unit of work
│   ├── dotnet/             # Options pattern, expression trees
│   ├── gof-behavior/       # 8 GoF behavioral patterns
│   ├── gof-creation/       # 3 GoF creational patterns
│   ├── gof-structure/      # 5 GoF structural patterns
│   └── security/           # Claims-based identity, guard clause
├── testing/                # 7 testing docs (French)
└── index.md
```

### Known problems

1. **Language**: 179/185 files in French — must be translated to English for
   open-source publication
2. **Mixed concerns**: framework reference, tutorials, patterns, and conventions
   are interleaved without clear separation by intent
3. **Pattern docs detached from usage**: 40+ pattern descriptions exist but are
   rarely cross-referenced from the module docs that implement them
4. **No explicit "why" layer**: design principles and philosophy are implied but
   not documented as a standalone section
5. **Duplicate quick starts**: both `guide/demarrage-rapide/` (8-step French) and
   `guide/getting-started.md` (English) exist with different approaches
6. **Convention docs are internal-facing**: `guide/conventions/` assumes contributor
   context, not consumer context
7. **ADRs in French**: need translation, and some lack the standard template fields
8. **No API reference generation**: no automated extraction from XML docs
9. **Cookbook is thin**: only 5 recipes for a 93-package framework
10. **No migration/changelog section**: versioning strategy is undocumented

### Content quality issues to audit

- **Non-compilable snippets**: `ct` used instead of `cancellationToken` in examples
- **Corrupted markdown**: placeholder text (`xxxxxxxxxx`), code outside fences
- **Governance contradictions**: some pages enforce "Minimal API only" while others
  show `app.MapControllers()`
- **DI anti-patterns in examples**: `BuildServiceProvider()` in configuration,
  non-generic `IOptions` usage
- **HTTP convention inconsistency**: `PUT` returns `200 OK` in some docs, `204 NoContent`
  in others — convention not explicit
- **Broken relative links**: references to pages that may not exist
- **Word-inherited table formatting**: non-standard Markdown tables

---

## Target documentation structure

The refactored documentation MUST follow this structure, inspired by Django
(structure), Kubernetes (mental model), Stripe (rigor), and Spring (opinionated
conventions):

```
docs/
├── index.md                        # Landing page: what is Granit, who is it for
│
├── getting-started/                # ADOPTION (5-minute path)
│   ├── index.md                    # Prerequisites, template install
│   ├── your-first-api.md           # granit-api template walkthrough
│   ├── adding-persistence.md       # EF Core + domain model
│   ├── adding-authentication.md    # JWT + Keycloak
│   ├── project-templates.md        # All 3 dotnet new templates: granit-api, granit-api-full, granit-module
│   └── next-steps.md               # Where to go from here
│
├── concepts/                       # UNDERSTANDING (read before coding)
│   ├── index.md                    # Framework philosophy & design principles
│   ├── module-system.md            # DependsOn, topological sort, IsEnabled
│   ├── dependency-injection.md     # Granit DI conventions, Options pattern
│   ├── configuration.md            # Configuration sources, options, settings
│   ├── multi-tenancy.md            # Strategies, isolation levels
│   ├── persistence.md              # DbContext pattern, interceptors, conventions
│   ├── messaging.md                # Wolverine optional, Channel fallback, outbox
│   ├── security-model.md           # Authentication, authorization, encryption
│   ├── compliance.md               # GDPR, ISO 27001 — what Granit enforces
│   ├── bundles.md                  # Meta-packages and the fluent builder
│   ├── wolverine-optionality.md    # What works without Wolverine
│   └── modular-monolith-vs-microservices.md  # Architecture comparison + Granit in both
│
├── guides/                         # HOW-TO (task-oriented, one intent per page)
│   ├── index.md
│   ├── create-a-module.md
│   ├── add-an-endpoint.md
│   ├── configure-multi-tenancy.md
│   ├── set-up-notifications.md
│   ├── implement-data-import.md
│   ├── add-background-jobs.md
│   ├── configure-blob-storage.md
│   ├── implement-webhooks.md
│   ├── add-feature-flags.md
│   ├── set-up-localization.md
│   ├── create-document-templates.md
│   ├── implement-workflow.md
│   ├── configure-caching.md
│   ├── add-api-versioning.md
│   ├── encrypt-sensitive-data.md
│   ├── implement-audit-timeline.md
│   ├── use-reference-data.md
│   ├── manage-application-settings.md
│   ├── configure-idempotency.md
│   └── end-to-end-tracing.md
│
├── reference/                      # EXHAUSTIVE (non-pedagogical)
│   ├── index.md                    # Package catalogue (93 packages, grouped)
│   ├── modules/                    # One page per module/package group
│   │   ├── core.md
│   │   ├── persistence.md
│   │   ├── security.md
│   │   ├── identity.md
│   │   ├── authorization.md
│   │   ├── notifications.md
│   │   ├── webhooks.md
│   │   ├── background-jobs.md
│   │   ├── data-exchange.md
│   │   ├── wolverine.md
│   │   ├── caching.md
│   │   ├── localization.md
│   │   ├── templating.md
│   │   ├── document-generation.md
│   │   ├── blob-storage.md
│   │   ├── imaging.md
│   │   ├── workflow.md
│   │   ├── timeline.md
│   │   ├── query-engine.md
│   │   ├── features.md
│   │   ├── settings.md
│   │   ├── reference-data.md
│   │   ├── validation.md
│   │   ├── observability.md
│   │   ├── api-surface.md          # Versioning, docs, CORS, idempotency, rate limiting, bulkhead
│   │   ├── privacy.md
│   │   └── multi-tenancy.md
│   ├── configuration-keys.md       # All appsettings sections, all options classes
│   ├── http-conventions.md         # Status codes, Problem Details, DTO naming
│   ├── dependency-graph.md         # Full package dependency visualization
│   └── provider-compatibility.md   # PostgreSQL / SQL Server / SQLite matrix
│
├── architecture/                   # DECISIONS & PATTERNS
│   ├── index.md                    # Architecture overview
│   ├── design-principles.md        # Convention over configuration, CQRS, etc.
│   ├── patterns/                   # Patterns AS USED in Granit (not generic theory)
│   │   ├── module-system.md
│   │   ├── transactional-outbox.md
│   │   ├── fan-out-delivery.md
│   │   ├── isolated-dbcontext.md
│   │   ├── channel-fallback.md
│   │   ├── interceptor-pipeline.md
│   │   ├── claim-check.md
│   │   ├── soft-delete.md
│   │   └── ...                     # All 40+ pattern docs — all actively used
│   └── adr/                        # Architecture Decision Records (already in English)
│       ├── index.md                # ADR log
│       ├── 001-observability.md
│       ├── 002-redis.md
│       └── ...
│
├── operations/                     # PRODUCTION (DevOps / SRE audience)
│   ├── index.md
│   ├── deployment.md               # Kubernetes, Docker, health checks
│   ├── configuration.md            # Vault, environment variables, secrets
│   ├── observability.md            # Serilog → Loki, OpenTelemetry → Tempo/Mimir
│   ├── ci-cd.md                    # Pipeline, dotnet format, tests, pack
│   ├── security-hardening.md       # TLS, CORS, rate limiting, API keys
│   └── production-checklist.md     # Go-live checklist
│
├── contributing/                   # CONTRIBUTOR (internal-facing)
│   ├── index.md
│   ├── coding-conventions.md       # Style, naming, architecture rules
│   ├── module-structure.md         # How to create a Granit package
│   ├── testing-conventions.md      # xUnit, Shouldly, NSubstitute, Bogus
│   ├── definition-of-done.md
│   ├── git-workflow.md
│   └── language-rules.md           # Code in English, issues in French
│
├── migration/                      # VERSIONING
│   ├── index.md
│   ├── changelog.md
│   └── upgrade-guides/             # Per-version breaking changes
│
└── troubleshooting/                # ERRORS & FAQ
    ├── index.md
    ├── common-errors.md
    ├── anti-patterns.md
    └── faq.md
```

---

## What the plan MUST contain

For each section of the target structure, the plan must specify:

1. **Source mapping**: which existing file(s) feed into this new page
2. **Content action**: translate / restructure / merge / create from scratch / delete
3. **Key transformations**: what changes beyond translation (e.g., "extract the 'why'
   section from persistence.md, move the configuration table to reference/")
4. **Cross-references**: which other pages this page must link to
5. **Priority**: P0 (blocks open-source launch), P1 (high value), P2 (nice to have)

Additionally, the plan must include:

- **Translation strategy**: batch translate vs. rewrite-from-scratch per section
- **Pattern docs**: all 40+ pattern docs are actively used — translate all of them
  and cross-reference each with the Granit module(s) that implement it
- **ADR handling**: all 16 ADRs are already in English — integrate them into the
  new structure, ensure they follow a consistent template
- **Static site generator recommendation**: recommend ONE tool among Docusaurus,
  MkDocs Material, VitePress, GitBook, or another — with a justified comparison. Evaluate
  on these criteria:
  - Markdown compatibility (frontmatter, admonitions/callouts, tabs, Mermaid diagrams)
  - Search quality (full-text, instant, offline-capable)
  - Versioned documentation support (multi-version docs for framework releases)
  - Navigation for large doc sets (93 packages, 10+ sections, 150+ pages)
  - API reference integration (auto-generated from .NET XML docs or TypeDoc-style)
  - Customization and theming (branding, dark mode, custom components)
  - Build performance at scale (150+ Markdown files)
  - Community and ecosystem maturity
  - Hosting simplicity (GitHub Pages, Netlify, Vercel, self-hosted)
  - i18n support (future-proofing — English first, French possible later)
  The recommendation must include a comparison table and a clear winner with rationale.
- **Site structure requirements**: the chosen tool must support:
  - Sidebar auto-generation from directory structure OR explicit sidebar config
  - Breadcrumb navigation
  - "Edit this page" links pointing to the GitHub repository
  - OpenGraph / SEO metadata per page
  - Custom admonition blocks for "Pro tip", "Good to know", "Warning" callouts
  - Code blocks with syntax highlighting, line numbers, and title/filename labels
  - Mermaid diagram rendering (inline, no external service)
  - Tabs for multi-provider examples (PostgreSQL / SQL Server side by side)
- **Validation checklist**: markdownlint, link checker, snippet compilation, build
  in CI (the site must build without warnings in GitHub Actions)
- **Phased execution order**: which sections to tackle first for maximum impact
- **Convention docs disposition**: which conventions are consumer-facing vs.
  contributor-only

---

## Tone and voice

The documentation targets **intermediate to senior .NET developers**. Do not explain
what dependency injection is. Do explain why Granit's DI conventions differ from
vanilla ASP.NET Core.

### Style principles

- **Engineer-to-engineer**: write as a colleague who has been through the trenches,
  not as a professor or a marketing team. Be direct, be precise, be honest about
  trade-offs.
- **Light humor, sparingly**: a well-placed dry remark or analogy keeps dense
  technical content engaging. Think Laravel's docs or Stripe's tone — never forced,
  never distracting. One per page maximum. If it does not land, cut it.
- **Tips and insights ("Pro tip", "Good to know")**: use callout blocks to share
  hard-won lessons, non-obvious shortcuts, or production gotchas. These are the
  details that make developers trust a framework's documentation. Examples:
  - "Pro tip: `TransactionMiddlewareMode.Eager` exists for a reason — `Lightweight`
    will pass your tests and fail your audit."
  - "Good to know: the Channel fallback handles ~10k messages/second in-process.
    You probably don't need Wolverine until you need durability guarantees."
- **No condescension**: never write "simply", "just", "obviously", or "it's easy to".
  If something were obvious, it would not need documentation.
- **No marketing language**: the framework is presented as an engineering product,
  not a sales pitch. State what it does, not how "powerful" or "blazing fast" it is.
- **Show, don't tell**: a 5-line code snippet is worth more than a paragraph of
  description. Lead with code, follow with explanation.
- **Opinionated but transparent**: when Granit makes a choice (CQRS, isolated
  DbContext, Channel fallback), explain the reasoning. Developers respect opinions
  when they come with receipts.

---

## Constraints

- **All content in English** — no French in the final output except proper nouns
- **No emojis** in documentation content
- **Code examples must compile** — use `cancellationToken` not `ct`, proper `using`s
- **Every page answers ONE user intent** — no "catch-all" pages
- **Explain WHY before HOW** — every concept page starts with the problem it solves
- **Conventions are explicit, not hidden** — document architectural choices or they
  don't exist
- **Pattern docs must reference Granit implementation** — no generic GoF theory
  without showing where/how Granit uses it
- **markdownlint clean** — all output must pass `npx markdownlint-cli2`
- **Compatible with Docs-as-Code** — structure must work with static site generators

---

## Quality criteria for each page

A page is acceptable if:

- It answers ONE user intent (concept, how-to, reference, or operation)
- It explains when to use it and why it exists
- It links to related concepts and reference pages
- It does not assume undocumented knowledge
- It remains valid across minor version updates
- Its code examples are complete and compilable
- It passes markdownlint

---

## Reference documentation sites (for style and structure)

Study these for inspiration — not to copy, but to match their quality level:

1. **Spring Boot** (spring.io/projects/spring-boot) — opinionated .NET-comparable
   framework, mature docs, design philosophy explicitly documented
2. **Django** (docs.djangoproject.com) — gold standard for Concepts > Guides >
   Reference separation
3. **Kubernetes** (kubernetes.io/docs) — mental model-first, mandatory concepts
   section, stability levels, governance
4. **Stripe** (docs.stripe.com) — rigor: compilable snippets, intent-based API docs
5. **React** (react.dev) — concept-first: every hook introduced by its problem
6. **Laravel** (laravel.com/docs) — elegant structure, progressive disclosure
7. **Next.js** (nextjs.org/docs) — App Router docs restructure, learn-by-doing
8. **Rust** (doc.rust-lang.org/book) — "The Book": progressive tutorial + reference
9. **Tailwind CSS** (tailwindcss.com/docs) — reference-heavy, excellent search

The target quality is a deliberate mix of:
Django (structure) + Kubernetes (governance) + Stripe (rigor) + Spring (conventions).

---

## Key content: Modular Monolith vs Microservices

The `concepts/modular-monolith-vs-microservices.md` page is a strategic content piece
designed to maximize audience reach. It MUST cover:

1. **The spectrum**: monolith, modular monolith, microservices — not binary, show the
   trade-offs along the continuum
2. **Granit's position**: Granit is designed for modular monoliths but its module
   isolation (DependsOn, isolated DbContext, Channel-based messaging) makes it a
   natural stepping stone toward microservices extraction
3. **Implementation: Modular Monolith** — show how Granit modules compose in a single
   deployable (GranitBuilder, bundles, shared process, Channel fallback)
4. **Implementation: Microservices** — show how to extract Granit modules into
   independent services (Wolverine for durable messaging, separate DbContext per
   service, multi-tenancy strategies, independent deployment)
5. **Migration path** — document the concrete steps to go from monolith to
   microservices using Granit (extract module, add Wolverine outbox, split database,
   deploy independently)
6. **Decision framework** — when to stay monolith, when to extract, what signals
   indicate the need to split

This page should include architecture diagrams (Mermaid) and reference real Granit
packages for each scenario.

---

## Deliverable

Produce a complete, actionable refactoring plan organized as a phased roadmap.
Include file-by-file source mapping, priority, content action, and execution order.
The plan should be executable by a team (human or AI) without further clarification.
