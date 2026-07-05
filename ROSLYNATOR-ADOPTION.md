# Roslynator adoption — migration runbook for Granit .NET repos

Self-contained procedure to bring a Granit .NET repo (business, showcase-dotnet,
microservice-template, website, iot, …) in line with `granit-dotnet`'s Roslynator setup.
Source of truth: `granit-dotnet` PRs #2928 (baseline), #2929 + #2930 + #2932 (promotions).
**#2932 made promotions repo-wide (tests included) and surfaced the SonarCloud interaction in
§1e — read it before you look at Sonar after Stage 1.**

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
#   RCS1194 implement exc ctors   → sealed single-shape domain exceptions (CA1032 is legacy)
#   RCS1201 use method chaining   → readability loss (activity.SetTag().SetTag() chains)
#   RCS1158 static in generic     → fires on const identifiers; CA1000 moot for consts
#   RCS1043 remove partial        → source-generator hazard ([LoggerMessage]/[GeneratedRegex])
#   RCS1124 inline local          → strips self-documenting snapshot locals; ugly casts
#   RCS1237 deprecated            → superseded by RCS1254
#   RCS1241 IEqualityComparer     → its fixer mangles ValueObject into an API change
dotnet_diagnostic.RCS1102.severity = none
dotnet_diagnostic.RCS1170.severity = none
dotnet_diagnostic.RCS1163.severity = none
dotnet_diagnostic.RCS1213.severity = none
dotnet_diagnostic.RCS1161.severity = none
dotnet_diagnostic.RCS1194.severity = none
dotnet_diagnostic.RCS1201.severity = none
dotnet_diagnostic.RCS1158.severity = none
dotnet_diagnostic.RCS1043.severity = none
dotnet_diagnostic.RCS1124.severity = none
dotnet_diagnostic.RCS1237.severity = none
dotnet_diagnostic.RCS1241.severity = none
# XML doc-comment family — off, consistent with NoWarn CS1591 (no full-XML-doc requirement):
dotnet_diagnostic.RCS1140.severity = none
dotnet_diagnostic.RCS1141.severity = none
dotnet_diagnostic.RCS1142.severity = none
dotnet_diagnostic.RCS1181.severity = none
dotnet_diagnostic.RCS1139.severity = none
dotnet_diagnostic.RCS1226.severity = none
# RCS1243 (duplicate word in comment) — corrupts repeated tokens like the "XXX XXX XXX" SIN mask.
dotnet_diagnostic.RCS1243.severity = none
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

### 1e. SonarCloud will show a one-time issue flood — expected, not a regression

The moment the Stage 1 baseline merges, the next SonarCloud analysis adds **hundreds to
~1000+ new MAINTAINABILITY issues**. This is a scanner artifact, not new debt:

- **SonarCloud imports external Roslyn diagnostics at *Info* severity**, not just warnings, as
  `external_roslyn:RCS*` issues. The advisory `suggestion` baseline surfaces the *entire*
  Roslynator RCS catalogue in the build's error log, and Sonar counts all of it.
- **The issues look pre-existing.** SonarCloud *backdates* issues from a newly-activated
  analyzer to each line's **last SCM commit date**, so they scatter across the repo's history
  instead of clustering at the analysis date. Do not conclude "these were already here."

In granit-dotnet this was a jump from ~6 to **1221** issues, **1215 of them `external_roslyn:RCS*`**.
Confirm the composition against the public API (no token needed for a public project):

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=<PROJECT_KEY>\
&impactSoftwareQualities=MAINTAINABILITY&issueStatuses=OPEN,CONFIRMED&facets=rules&ps=1" \
  | python3 -c "import sys,json; f=json.load(sys.stdin)['facets'][0]['values']; \
[print(f'{v[\"count\"]:5}  {v[\"val\"]}') for v in sorted(f, key=lambda x:-x['count'])]"
```

The flood recedes as Stage 2 promotes-and-fixes rules and the disables above take effect.
**Two things matter:**

- **Promote Stage 2 rules repo-wide (`[*.{cs,csx}]`, tests included), not src-only.** In
  granit-dotnet ~81 % of the flood was in test projects precisely because the first promotions
  were scoped `[src/**.cs]` while the baseline (and Sonar's import) covers the whole repo.
- If you cannot run Stage 2 promptly, do **not** hide the issues by excluding tests from Sonar —
  suppress the imported external rules in the scanner config instead, so real Sonar rules on
  tests still run:

  ```text
  /d:sonar.issue.ignore.multicriteria=e_ros
  /d:sonar.issue.ignore.multicriteria.e_ros.ruleKey=external_roslyn:RCS*
  /d:sonar.issue.ignore.multicriteria.e_ros.resourceKey=**/*
  ```

## Stage 2 — Promotions (per repo, after Stage 1 is merged)

For each rule below: auto-fix → verify 0 remaining → add the `.editorconfig` line under a
`[*.{cs,csx}]` section (enforced **repo-wide, tests included** — scoping to `[src/**.cs]`
leaves the tests at the `suggestion` baseline and recreates the Sonar test-file flood from
§1e) → build green on a **test-bearing** shard → commit.

```bash
# <TARGET> = a .slnf shard (big repos) or the whole .slnx (small repos).
# NOTE: --diagnostics is SPACE-separated, NOT comma-separated.
dotnet format analyzers <TARGET> --diagnostics RCS1214 --severity info
dotnet format analyzers <TARGET> --diagnostics RCS1214 --severity info --verify-no-changes   # expect 0
# add the line to .editorconfig, then:
dotnet build <TARGET>    # 0/0 under TreatWarningsAsErrors == rule is clean & enforced
```

### The `[*.{cs,csx}]` block to build up (~32 rules, as in granit-dotnet after #2932)

```ini
[*.{cs,csx}]
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
# added in #2932 (RCS1093 in follow-up #2933):
dotnet_diagnostic.RCS1047.severity = warning   # drop 'Async' suffix on non-async methods
dotnet_diagnostic.RCS1112.severity = warning   # combine consecutive Enumerable.Where
dotnet_diagnostic.RCS1135.severity = warning   # declare zero-value member on [Flags] enum
dotnet_diagnostic.RCS1199.severity = warning   # remove unnecessary null check
dotnet_diagnostic.RCS1247.severity = warning   # fix malformed doc-comment tag (<c> vs <code>)
dotnet_diagnostic.RCS1093.severity = warning   # file contains no code (delete dead file first)
dotnet_diagnostic.RCS1015.severity = warning   # use nameof
dotnet_diagnostic.RCS1033.severity = warning   # remove redundant '== true'
dotnet_diagnostic.RCS1049.severity = warning   # simplify boolean comparison ('x == false' → '!x')
dotnet_diagnostic.RCS1097.severity = warning   # remove redundant ToString
```

### Fixer gotchas (hit these in granit-dotnet — expect them again)

Some rules have **no Fix-All action** — `dotnet format` reports "didn't return a Fix All action"
and leaves the hit in place. Collect the stragglers with a repo-wide
`dotnet format analyzers <shard> --diagnostics <rules> --verify-no-changes` pass and fix them by
hand before the build will go green. In granit-dotnet #2932: RCS1047 (renames), RCS1135 (add
`None = 0`), RCS1077 (`.Where(p).Count()`→`.Count(p)`, `.OrderBy(x=>x)`→`.Order()`), RCS1112.

- **RCS1077 / RCS1112**: the fixer leaves a `;` alone on its own line (`dotnet format whitespace`
  does NOT reflow it — join it by hand) and produces `var` where `ConvertAll` hides the type →
  fix IDE0008 by turning `var` into the explicit `List<T>`.
- **RCS1084**: can emit an invalid `(T??)` cast (seen twice — `(object??)`, `(BlobReference??)`) →
  replace by hand (the ternary is usually redundant, e.g. `x is not null ? x : null` → just `x`;
  a deliberately-"bad" test lambda needs a differently-shaped non-property expression).
- **RCS1047**: false-positives when the `Async` suffix disambiguates two overloads that differ
  only by generic constraint (they cannot share a name → CS0111). Suppress inline with
  `[SuppressMessage("Roslynator", "RCS1047:…")]`, do NOT rename.
- **RCS1093**: has no auto-fix — it flags a comment-only/empty file. **Delete the file yourself**
  (and confirm nothing links it) *before* promoting the rule, else the build cannot go green.
- **RCS1206**: may leave a non-fixable nullable-value case (`JsonElement?`) → convert by hand
  (`data?.Deserialize<…>()`).
- **RCS1261**: for any `XmlWriter` conversion, confirm `XmlWriterSettings.Async = true`,
  otherwise `DisposeAsync()` throws at runtime — leave those as plain `using`. Note that
  `await using` disposal carries no `ConfigureAwait(false)` (moot under ASP.NET Core).

### DO NOT promote (already disabled in the Stage 1 baseline)

Every rule that fights a Granit pattern is set to `none` in the §1c baseline block, so there is
nothing to re-evaluate at Stage 2: RCS1102/1170/1163/1213/1161, RCS1194/1201/1158/1043/1124/
1237/1241, the XML-doc family (RCS1140/1141/1142/1181/1139/1226), RCS1243, RCS1217. Rationale is
inline there. Highlights worth remembering:

- **RCS1241** — its fixer bolts a non-generic `IEqualityComparer` onto `ValueObject` with
  `new public bool Equals(object, object)` + empty-message throws: an API/semantic change on a
  core domain type. Same family as the ValueObject/Entity/AggregateRoot interface-impl Won't-Fix.
- **RCS1243** — corrupts repeated tokens (`XXX XXX XXX` SIN mask → `XXX XXX`), a silent doc bug.
- **RCS1043** — would strip `partial` the source generators require (`[LoggerMessage]`, `[GeneratedRegex]`).

Two rules that *look* like this rejected batch but ARE promoted (see the block above), with a caveat:

- **RCS1047** (drop `Async` suffix) — promoted; suppress the overload-disambiguation false positive
  inline (see gotchas), do not rename.
- **RCS1247** (fix doc-comment tag) — promoted; it only normalizes `<c>`/`<code>`, it does not
  demand doc completeness (that family stays disabled).

### Final gates before MR/PR

```bash
dotnet format <TARGET> --verify-no-changes    # exit 0
dotnet build  <a test-bearing shard>          # 0/0 (src + tests)
```

## Stage 3 — Sonar closeout (per repo, after Stage 2 is merged)

Goal: drive the repo's Sonar `external_roslyn:RCS*` **MAINTAINABILITY** backlog (the §1e
flood) to **~0**, without losing the advisory Roslynator baseline in-IDE. The lever is the
same as Stage 2 — promote or disable each rule — applied to *everything still firing* rather
than a curated list.

### Pre-check — does the repo run Sonar?

Grep the CI (`sonar` in `.gitlab-ci.yml` / `.github/workflows`). **No Sonar job → Stage 3 is
moot, stop.** Note the project key (`/k:` in the `sonarscanner begin` command — often
`${CI_PROJECT_NAME}`) and the host (SonarCloud *or* a self-hosted SonarQube).

### 1. Measure the backlog

Ventilation by rule (SonarCloud public project → no token; self-hosted / private → a **user**
token with *Browse*, **not** the CI analysis token, which is 401 on the Web API):

```bash
curl -s -H "Authorization: Bearer <USER_TOKEN>" "<HOST>/api/issues/search?componentKeys=<KEY>\
&impactSoftwareQualities=MAINTAINABILITY&issueStatuses=OPEN,CONFIRMED&facets=rules&ps=1" \
  | python3 -c "import sys,json; f=json.load(sys.stdin)['facets'][0]['values']; \
[print(f'{v[\"count\"]:5}  {v[\"val\"]}') for v in sorted(f, key=lambda x:-x['count'])]"
```

No API access? §1e says the count **is** Roslynator's Info-level output, so reproduce it
locally: temporarily set `category-Roslynator.severity = warning`, `dotnet build --no-incremental
-p:TreatWarningsAsErrors=false`, and `grep -oE "warning RCS[0-9]+" | sort | uniq -c`. Revert the
edit. Note the src-vs-tests split — Stage 2's `[src/**.cs]` scope leaves the whole test side
firing.

### 2. Triage each firing `RCS*` rule

- **Promote** (`→ warning`, in `[*.{cs,csx}]` **repo-wide, tests included** — not `[src/**.cs]`;
  see §1e) when it is a genuine auto-fixable improvement. Auto-fix per shard, hand-fix the
  non-Fix-All stragglers (runbook gotchas apply).
- **Disable** (`→ none`, inline justification) when it fights a Granit pattern or is
  subjective. **Reuse the granit-dotnet decisions verbatim** — RCS1194/1201/1158/1043/1124/
  1237/1241/1139/1226/1243 — do not re-debate.
- **Repo-specific rule** (fires here but not in granit-dotnet): decide on merits and flag it in
  the MR if it touches a convention.

### 3. Deliberate advisory remainder (only if any)

If you *intentionally* leave rules at `suggestion` (advisory in-IDE, not promoted, not hostile),
suppress the imported external rule scanner-side so Sonar stops gating on advisory diagnostics —
keep native Sonar rules on tests:

```text
/d:sonar.issue.ignore.multicriteria=e_ros
/d:sonar.issue.ignore.multicriteria.e_ros.ruleKey=external_roslyn:RCS*
/d:sonar.issue.ignore.multicriteria.e_ros.resourceKey=**/*
```

If every firing rule was promoted or disabled, this step is unnecessary (granit-dotnet and
granit-showcase-dotnet both reached 0 without it).

### Repo-specific gotchas (hit in granit-showcase-dotnet — apps ship EF migrations; granit-dotnet does not)

- **Generated EF migrations turn the build red.** A `[src/**.cs]` (or `[*.{cs,csx}]`) promotion
  also enforces `src/**/Migrations/**`, flooding ~hundreds of RCS1205/RCS1021 errors on
  scaffolded code. Carve them out under `[**/Migrations/**/*.cs]` with a **per-rule** `= none`
  for every promoted rule — a category-level `category-Roslynator = none` **loses** to the
  specific-rule promotion and does nothing.
- **The CI `SONAR_TOKEN` is an analysis token** — it returns 401 (`valid:false`) on the Web
  API. Use a user token to measure, or the local Roslyn-at-Info proxy above.

### Final gates before MR/PR

```bash
# re-run the Sonar query (or the local proxy) → external_roslyn:RCS* ≈ 0
dotnet build  <a test-bearing shard>          # 0/0 under TreatWarningsAsErrors
dotnet format <TARGET> --verify-no-changes    # exit 0
```

Ship one **"Stage 3 — Sonar closeout"** MR/PR per repo, green, with the before/after Sonar
counter in the description.

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
