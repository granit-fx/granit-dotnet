# Roslynator adoption — migration runbook for Granit .NET repos

Self-contained procedure to bring a Granit .NET repo (business, showcase-dotnet,
microservice-template, website, iot, …) in line with `granit-dotnet`'s Roslynator setup.
Source of truth: `granit-dotnet` PRs #2928 (baseline), #2929 + #2930 (promotions).

This is a maintainer runbook — paste the relevant stage into a session working **inside the
target repo**.

## Ground rules

- **Stage 1 (Baseline) is non-breaking** — do it in every .NET repo. Ship it alone in one MR/PR.
- **Stage 2 (Promotions) is breaking until the code is auto-fixed** — do it only after Stage 1
  is merged, one repo at a time, verifying a green build at each rule.
- **Never build/format the full solution if Roslyn OOMs** (large repos like business). Use the
  repo's `.slnf` shards; on small repos (≤ ~10 projects: showcase, microservice) the whole
  `.slnx` is fine.
- **Workflow**: GitLab repos (business, showcase-dotnet, website, iot) → `glab`, MR to the repo's
  default integration branch, **commits/MR in English**. GitHub repos (microservice-template) →
  `gh`, PR to `develop`. Always check PR/MR state before pushing to an existing branch.
- Lock files: these repos use `RestorePackagesWithLockFile=true` → after any package change run
  `dotnet restore <Sln>.slnx --force-evaluate` and commit every regenerated `packages.lock.json`.
- If the repo maintains `THIRD-PARTY-NOTICES.md`, add `Roslynator.Analyzers | 4.15.0 |
  Copyright (c) Josef Pihrt` under **Apache-2.0** and bump the Apache count.

## Stage 1 — Baseline (non-breaking, do everywhere)

### 1a. `Directory.Packages.props` — pin the package

```xml
<!-- Roslynator: ~200 code-quality analyzers (RCS1xxx). Patch-only float so a new minor
     (which ships new rules) never silently changes the baseline. Formatting analyzers
     (Roslynator.Formatting.Analyzers, RCS0xxx) are deliberately NOT referenced — they
     conflict with `dotnet format`. Severity governed centrally in .editorconfig. -->
<PackageVersion Include="Roslynator.Analyzers" Version="4.15.*" />
```

### 1b. `Directory.Build.props` — global dev-time reference

Add inside `<Project>`. The condition excludes analyzer/source-generator projects (netstandard,
Roslyn-pinned); **on repos with no such projects (showcase, microservice) drop the `Condition`**.

```xml
<!-- Roslynator.Analyzers: general C# code-quality analyzers (RCS1xxx).
     Baseline severity is 'suggestion' (see .editorconfig) so it never trips
     TreatWarningsAsErrors; promote individual rules to warning as the tree is cleaned. -->
<ItemGroup Condition="!$(MSBuildProjectName.Contains('Analyzers')) And
                      !$(MSBuildProjectName.Contains('SourceGenerator'))">
  <PackageReference Include="Roslynator.Analyzers" PrivateAssets="all">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

### 1c. `.editorconfig` — baseline + disables

If the repo has **no `.editorconfig`** (e.g. microservice-template), create one at the repo root
starting with `root = true` and a `[*.{cs,csx}]` section, then add this block inside it.

```ini
# ── Roslynator.Analyzers (RCS1xxx) ────────────────────────────────────────────
# Baseline: advisory only. 'suggestion' surfaces in-IDE and in build output but never
# escalates under TreatWarningsAsErrors. Promote individual rules to 'warning' (Stage 2)
# once the tree is verified clean for that rule (a warning == a build error here).
dotnet_analyzer_diagnostic.category-Roslynator.severity = suggestion

# Disabled because they fight deliberate Granit patterns (harmless to keep even where the
# pattern is absent):
#   RCS1102 make class static     → Wolverine handlers must be non-static public classes
#   RCS1170 read-only auto-prop   → DDD { get; private set; } + EF reflection materialization
#   RCS1163 unused parameter      → Wolverine Handle sigs / DI factories / endpoint handlers
#   RCS1213 remove unused member  → EF private ctors, DI/reflection-invoked members
#   RCS1161 enum explicit values  → contradicts the GRENUM001 analyzer
dotnet_diagnostic.RCS1102.severity = none
dotnet_diagnostic.RCS1170.severity = none
dotnet_diagnostic.RCS1163.severity = none
dotnet_diagnostic.RCS1213.severity = none
dotnet_diagnostic.RCS1161.severity = none
# XML doc-comment family — off, consistent with NoWarn CS1591 (no full-XML-doc requirement):
dotnet_diagnostic.RCS1140.severity = none
dotnet_diagnostic.RCS1141.severity = none
dotnet_diagnostic.RCS1142.severity = none
dotnet_diagnostic.RCS1181.severity = none
# RCS1217 (interpolation → concatenation) — opposite of the promoted RCS1267; off to avoid a pair.
dotnet_diagnostic.RCS1217.severity = none
# RCS1090 (add ConfigureAwait) — kept at suggestion (correct for lib code, noise in Endpoints);
# silence it in tests via a [tests/**.cs] section:  dotnet_diagnostic.RCS1090.severity = none
```

Add a tests carve-out (adjust the glob to the repo's test dir):

```ini
[tests/**.cs]
dotnet_diagnostic.RCS1090.severity = none
```

### 1d. Verify Stage 1

```bash
dotnet restore <Sln>.slnx --force-evaluate          # regenerate lock files
dotnet build  <shard>.slnf   # or the whole .slnx on small repos — MUST stay 0 warning / 0 error
```

Baseline is `suggestion`, so a green build is expected; if it turns red, a Roslynator rule
defaulted to warning — set that specific rule to `suggestion` or `none`. Commit, open MR/PR.

## Stage 2 — Promotions (per repo, after Stage 1 is merged)

For each rule below: auto-fix → verify 0 remaining → add the `.editorconfig` line under a
`[src/**.cs]` section (framework source enforced, tests stay advisory) → build green → commit.

```bash
# <TARGET> = a .slnf shard (big repos) or the whole .slnx (small repos).
# NOTE: --diagnostics is SPACE-separated, NOT comma-separated.
dotnet format analyzers <TARGET> --diagnostics RCS1214 --severity info
dotnet format analyzers <TARGET> --diagnostics RCS1214 --severity info --verify-no-changes   # expect 0
# add the line to .editorconfig, then:
dotnet build <TARGET>    # 0/0 under TreatWarningsAsErrors == rule is clean & enforced
```

### The `[src/**.cs]` block to build up (23 rules, exactly as in granit-dotnet)

```ini
[src/**.cs]
dotnet_diagnostic.RCS1214.severity = warning   # unnecessary interpolated string
dotnet_diagnostic.RCS1192.severity = warning   # unnecessary verbatim string
dotnet_diagnostic.RCS1146.severity = warning   # use conditional access ?.
dotnet_diagnostic.RCS1118.severity = warning   # mark local as const
dotnet_diagnostic.RCS1085.severity = warning   # use auto-property { get; }
dotnet_diagnostic.RCS1077.severity = warning   # List.Select().ToList() → ConvertAll()
dotnet_diagnostic.RCS1197.severity = warning   # optimize StringBuilder.Append/AppendJoin
dotnet_diagnostic.RCS1123.severity = warning   # add clarifying parentheses
dotnet_diagnostic.RCS1205.severity = warning   # order named arguments
dotnet_diagnostic.RCS1021.severity = warning   # expression-bodied lambda
dotnet_diagnostic.RCS1251.severity = warning   # empty type body → ;
dotnet_diagnostic.RCS1073.severity = warning   # redundant if → return
dotnet_diagnostic.RCS1221.severity = warning   # pattern matching over as + null
dotnet_diagnostic.RCS1235.severity = warning   # optimize dictionary Add
dotnet_diagnostic.RCS1249.severity = warning   # drop unnecessary null-forgiving !
dotnet_diagnostic.RCS1212.severity = warning   # remove redundant assignment
dotnet_diagnostic.RCS1206.severity = warning   # conditional access over ?:
dotnet_diagnostic.RCS1084.severity = warning   # ?? over ?:
dotnet_diagnostic.RCS1173.severity = warning   # ?? over if
dotnet_diagnostic.RCS1006.severity = warning   # merge else + nested if → else if
dotnet_diagnostic.RCS1196.severity = warning   # extension method as instance method
dotnet_diagnostic.RCS1267.severity = warning   # interpolation over string.Concat
dotnet_diagnostic.RCS1261.severity = warning   # using → await using (async dispose)
```

### Fixer gotchas (hit these in granit-dotnet — expect them again)

- **RCS1077**: the fixer leaves a `;` alone on its own line (`dotnet format` does NOT reflow it —
  join it by hand) and produces `var` where `ConvertAll` hides the type → fix IDE0008 with
  `dotnet format` on the affected files (turns `var` into the explicit `List<T>`).
- **RCS1084**: can emit an invalid `(object??)` cast → replace by hand (the ternary is usually
  redundant, e.g. `x is not null ? x : null` → just `x`).
- **RCS1206**: may leave a non-fixable nullable-value case (`JsonElement?`) → convert by hand
  (`data?.Deserialize<…>()`).
- **RCS1261**: for any `XmlWriter` conversion, confirm `XmlWriterSettings.Async = true`,
  otherwise `DisposeAsync()` throws at runtime — leave those as plain `using`. Note that
  `await using` disposal carries no `ConfigureAwait(false)` (moot under ASP.NET Core).

### DO NOT promote (evaluated and rejected)

- **RCS1243** (duplicate word in comment) — **corrupts** repeated format placeholders
  (`XXX XXX XXX` → `XXX XXX`), a silent doc bug. Leave at suggestion.
- **RCS1124** (inline single-use local) — strips self-documenting `snapshot` locals, emits
  `(MemoryStream)new()` casts. Net readability loss.
- **RCS1201** (method chaining), **RCS1194** (exception-ctor boilerplate), **RCS1047** (removes
  `Async` suffix = API break), **RCS1158** (design), **RCS1247/RCS1139** (doc family).

### Final gates before MR/PR

```bash
dotnet format <TARGET> --verify-no-changes    # exit 0
dotnet build  <a test-bearing shard>          # 0/0 (src + tests)
```

## Per-repo notes

All five are `TreatWarningsAsErrors=true`, `src/` layout, no Roslynator yet.

| Repo | Host → MR/PR base | Fix/build target | Special |
| ---- | ----------------- | ---------------- | ------- |
| **granit-business** | GitLab → `develop` | its `.slnf` shards (143 projects — **never** full `.slnx`) | biggest effort; may have its own analyzer projects → **keep the `Condition`**; `.editorconfig` (98 l) present; update `THIRD-PARTY-NOTICES.md` |
| **granit-website** | GitLab → `develop` | its 4 `.slnf` shards (`.github/shard-filters/`) — 29 projects | `.editorconfig` (98 l) present; `THIRD-PARTY-NOTICES.md` present |
| **granit-iot** | GitLab → `develop` | whole `Granit.IoT.slnx` (24 projects; **no shards** — watch memory, shard if Roslyn OOMs) | `.editorconfig` (51 l) present; `THIRD-PARTY-NOTICES.md` present |
| **granit-showcase-dotnet** | GitLab → `main` | whole `granit-showcase.slnx` (4 projects) | drop the `Condition`; `.editorconfig` (54 l) present |
| **granit-microservice-template** | GitHub → `develop` | whole `GranitMicroservice.slnx` (8 projects) | **no `.editorconfig` — create one** (`root = true`); drop the `Condition`; no `THIRD-PARTY-NOTICES.md` |

React repos (granit-front, granit-showcase-react) are out of scope (not .NET).

All five repos are checked out locally under `~/dev/granit-fx/<repo>` — run the procedure
in-repo. Business/website/iot commit in English, MR via `glab`.
