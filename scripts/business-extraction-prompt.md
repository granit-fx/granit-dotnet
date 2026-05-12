# Prompt: create the `granit-business` private repo

Drop this entire file into a fresh Claude Code session started with `cd ~/dev/granit-fx/granit-dotnet`. It is self-contained — no prior conversation context is assumed.

---

## Goal

Extract the application-level business modules of the Granit framework out of the open-source `granit-dotnet` repo into a **new private commercial repo** named `granit-business` (under the same GitHub org, `granit-fx/granit-business`). Preserve full git history of every extracted file via `git filter-repo`. Bootstrap the new repo so its CI publishes the **same NuGet package IDs** (e.g. `Granit.Invoicing`, `Granit.Documents`) at **continuing version numbers** — no breaking change for downstream consumers (`granit-showcase-dotnet` already uses `PackageReference`, so it just keeps working once `granit-business` publishes its first nupkgs).

## Constraints

- **Versioning**: continuity (option A1). The first `granit-business` publish continues the `0.1.0-dev.N` scheme already in use by `granit-dotnet/Directory.Build.props`. Keep `VersionPrefix=0.1.0` initially.
- **License**: `granit-business` is **proprietary, all-rights-reserved**. Write a fresh `LICENSE` file (no Apache-2.0). The historical commits that were Apache-2.0 in `granit-dotnet` remain Apache-2.0 in those commits — that is accepted and irreversible (clarified in pre-extraction discussion).
- **Default branch**: `develop` (matches `granit-dotnet` and `granit-docs`).
- **GitFlow**: same conventions as `granit-dotnet` — `feature/*` and `fix/*` → `develop`; `hotfix/*` and `release/*` → `main` + `develop`.
- **Cross-repo dependency**: `granit-business` modules consume framework modules from `granit-dotnet` **via NuGet** (`PackageReference`), never `ProjectReference`. This is the boundary.
- **No tag** on the last `granit-dotnet` commit-before-split (user opted out).

## Path inventory (source of truth)

The exhaustive path list lives in [`scripts/list-business-paths.sh`](scripts/list-business-paths.sh) at the current `granit-dotnet/develop` HEAD. As of the latest run it emits **255 paths** covering:

- 15 module roots (`Granit.Activities`, `Granit.Analytics`, `Granit.Catalog`, `Granit.CustomerBalance`, `Granit.Dashboards`, `Granit.Documents`, `Granit.Entities`, `Granit.Invoicing`, `Granit.Metering`, `Granit.Parties`, `Granit.Payments`, `Granit.ReferenceData`, `Granit.Subscriptions`, `Granit.Tax`, `Granit.Taxonomy`, `Granit.Workspaces`) — each with all their `.Endpoints`, `.EntityFrameworkCore`, `.BackgroundJobs`, `.Notifications`, sub-modules
- 2 bundles (`Granit.Bundle.Documents`, `Granit.Bundle.SaaS`)
- 6 observability satellites (`Granit.{BlobStorage,Webhooks}.{Analytics,Dashboards}`, `Granit.Notifications.Analytics`, `Granit.Identity.Federated.Analytics`)
- All matching `tests/` projects

Retained in framework (NOT extracted): `Granit.Analytics.Abstractions`, `Granit.Entities.Abstractions` (declarative contracts the framework's `Granit.Identity`, `Granit.OpenIddict`, `Granit.BlobStorage` etc. depend on).

## Step-by-step execution

### Step 1 — Refresh the path list

```bash
cd ~/dev/granit-fx/granit-dotnet
scripts/list-business-paths.sh --count        # expect 255 (may grow over time)
scripts/list-business-paths.sh > /tmp/business-paths.txt
wc -l /tmp/business-paths.txt
```

### Step 2 — Mirror clone (preserves all branches + tags)

Do **not** work on the current working tree. Make a fresh mirror clone in a sibling location:

```bash
git clone --no-local ~/dev/granit-fx/granit-dotnet ~/dev/granit-fx/granit-business
cd ~/dev/granit-fx/granit-business
git remote remove origin     # detach from granit-dotnet
```

### Step 3 — `git filter-repo` to keep only business paths

Install `git-filter-repo` if missing (`pip install git-filter-repo` or `apt install git-filter-repo`).

```bash
cd ~/dev/granit-fx/granit-business
git filter-repo --paths-from-file /tmp/business-paths.txt --force
```

This rewrites every commit to drop non-business files, preserving authorship, dates, and SHAs are recalculated. Verify post-rewrite:

```bash
git log --oneline | wc -l                    # expect thousands of commits
ls src/                                       # only business modules + satellites
ls -d src/Granit.{Identity,BlobStorage,Webhooks,Notifications,OpenIddict,Vault,Localization} 2>&1   # MUST report "No such file or directory" for these — they belong to the framework
```

If any framework module survived, the script over-includes or filter-repo command was wrong — STOP and report before proceeding.

### Step 4 — Bootstrap the new repo

The mirror inherits `Directory.Build.props`, `.editorconfig`, `nuget.config`, hooks, GitHub Actions, etc. Adjust only what differs:

#### 4a. Replace `LICENSE` (Apache-2.0 → proprietary)

```bash
cat > LICENSE << 'EOF'
Copyright (c) 2026 Digital Dynamics SRL. All rights reserved.

This software and its source code are proprietary and confidential. Unauthorized
copying, modification, distribution, or use of this software, via any medium, is
strictly prohibited without the express written permission of Digital Dynamics SRL.

Commercial licensing terms are available on request: legal@digitaldynamics.be.
EOF
```

Also remove or rewrite any `LICENSE-MIT`, `LICENSE-APACHE` variants that may have been present.

#### 4b. Update `CLAUDE.md`

The mirror carries `granit-dotnet/CLAUDE.md` verbatim. Adapt:

- Project type → "Commercial application modules built on top of `granit-dotnet`"
- License → "Proprietary, all rights reserved (commercial edition)"
- Cross-repo references → `granit-business` consumes `granit-dotnet` via NuGet
- Drop sections that describe framework-only modules
- Keep all coding standards (C# 14, .NET 10, EF Core, DDD, declarative definitions placement, etc.)

#### 4c. Update `Directory.Build.props`

Keep `VersionPrefix=0.1.0` / `VersionSuffix=dev` for continuity. Already aligned.

#### 4d. Clean Solution + filters

```bash
# Regenerate Granit.slnx using the same Python helpers (they live in scripts/)
# But filter-repo dropped framework projects from Granit.slnx — most lines now
# point to non-existent paths. Easiest: rebuild slnx from scratch.
python3 scripts/reorganize-slnx.py   # or rebuild from `find src tests -name '*.csproj'`
python3 scripts/generate-shard-filters.py
python3 scripts/generate-domain-filters.py
python3 scripts/generate-code-index.py
```

If the scripts depend on framework data (they should not), adapt or simplify.

#### 4e. Fix `*.csproj` `ProjectReference` to framework modules

After filter-repo, business modules still reference paths like `..\Granit\Granit.csproj` and `..\Granit.Identity\Granit.Identity.csproj` — these files no longer exist in `granit-business`. Convert each cross-boundary ref to a `PackageReference`:

```bash
# Script-driven: for each ProjectReference pointing outside src/ (i.e. to a framework module),
# replace with PackageReference Include="<PackageName>" Version="$(GranitDotnetVersion)".
```

Recommended: add a `<PackageVersion>` `<ItemGroup>` in `Directory.Packages.props` (if using central package management) or `Directory.Build.props` pinning the granit-dotnet version we depend on.

#### 4f. Verify build

```bash
dotnet restore --force-evaluate
dotnet build Granit.slnx
```

This **will fail** until the granit-dotnet NuGet packages corresponding to each `<PackageReference>` are published and available. For the first dry-run, point `nuget.config` at a local `~/dev/granit-fx/granit-dotnet/nupkgs` folder generated by `dotnet pack` in the framework repo.

### Step 5 — GitHub repo creation

```bash
gh repo create granit-fx/granit-business \
  --private \
  --description "Granit framework — commercial business modules (invoicing, payments, subscriptions, documents, parties, dashboards, analytics, …)" \
  --source ~/dev/granit-fx/granit-business \
  --remote origin

cd ~/dev/granit-fx/granit-business
git push --all origin
git push --tags origin

# Set default branch
gh api -X PATCH repos/granit-fx/granit-business -f default_branch=develop
```

### Step 6 — CI / publish pipeline

Adapt `.github/workflows/*.yml` from granit-dotnet:

- Build matrix: same shards as granit-dotnet (filter-repo kept the shard files)
- Publish step: target a private NuGet feed (Azure Artifacts, GitHub Packages, or a custom feed). NEVER publish to nuget.org from `granit-business` (the packages are proprietary).
- Secrets: configure `NUGET_API_KEY` and `GITHUB_TOKEN` via `gh secret set` if needed.

### Step 7 — First publish dry-run

Trigger the CI manually (`gh workflow run`) and verify the first set of nupkgs arrives at the private feed. Confirm versions are `0.1.0-dev.N` continuing from where granit-dotnet left off.

### Step 8 — Verify with `granit-showcase-dotnet`

Switch the showcase's `nuget.config` to add the new private feed. Run `dotnet restore && dotnet build` in `~/dev/granit-fx/granit-showcase-dotnet`. If it builds, the boundary works.

## What to skip / leave alone

- Don't touch `granit-dotnet` in this session — the cleanup (removing the business modules from `granit-dotnet`) is a separate PR handled in the originating session.
- Don't publish to nuget.org from `granit-business` — proprietary.
- Don't try to rewrite history on `granit-dotnet` — user decided against scrubbing (the Apache-2.0 commits stay public).
- Don't create migration helpers, fallback shims, or `[Obsolete]` markers — `granit-business` is a new repo, clean break.

## Reporting back

When done, return a one-paragraph status:
- Number of commits preserved in `granit-business`
- Whether the build passes (and on what feed config)
- URL of the new private repo
- First CI run URL
- Any blockers encountered

---
