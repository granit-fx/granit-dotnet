#!/usr/bin/env bash
# Enumerate every src/ and tests/ project that belongs to the business edition
# (extracted to the granit-business repo).
#
# Output: one path per line, suitable for `git filter-repo --path <p>` (or
# `--paths-from-file`). Run from the repo root.
#
# Retained in framework (NOT emitted, even though the prefix matches):
#   - src/Granit.Analytics.Abstractions   (declarative contracts: MetricDefinition, widgets)
#   - tests/Granit.Analytics.Abstractions.Tests
#   - src/Granit.Entities.Abstractions    (declarative contracts: EntityDefinition, builders)
#   - tests/Granit.Entities.Abstractions.Tests
#
# IMPORTANT — additional paths the extraction must include but which STAY in
# granit-dotnet (so they are NOT emitted by this script — concatenate manually
# when feeding `git filter-repo --paths-from-file`):
#
#   Root build/infra :  .claude .editorconfig .github .gitignore .gitleaks.toml
#                       .husky .markdownlint-cli2.jsonc .markdownlint.json
#                       .markdownlintignore .semgrepignore .slnf .vscode
#                       BannedSymbols.txt CHANGELOG.md CLAUDE.md
#                       CODE_OF_CONDUCT.md CONTRIBUTING.md
#                       Directory.Build.props Directory.Packages.props
#                       Granit.slnx LICENSE NOTICE README.md SECURITY.md
#                       THIRD-PARTY-NOTICES.md coverage.runsettings
#                       dotnet-tools.json global.json nuget.config scripts
#   Tests-root config :  tests/Directory.Build.props  tests/xunit.runner.json
#   Shared assets :      assets/granit-icon-64.png
#
# Discovered missing during the first extraction (May 2026) — without them,
# the new granit-business repo can't build: tests can't resolve junit logger
# or xunit.runner.json, pack fails on missing PackageIcon asset, etc.
#
# Usage:
#   scripts/list-business-paths.sh                 # human-readable list
#   scripts/list-business-paths.sh --filter-repo   # emit `--path X` lines
#   scripts/list-business-paths.sh --paths-file    # plain list for --paths-from-file

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

# Module roots that move to granit-business. Order = display order.
ROOTS=(
  Granit.Activities
  Granit.ReferenceData
  Granit.Taxonomy
  Granit.Analytics
  Granit.Dashboards
  Granit.Metering
  Granit.Catalog
  Granit.CustomerBalance
  Granit.Invoicing
  Granit.Payments
  Granit.Subscriptions
  Granit.Tax
  Granit.Parties
  Granit.Documents
  Granit.Workspaces
  Granit.Entities
)

# Exact project directory names kept in framework (the Abstractions split).
RETAIN=(
  "src/Granit.Analytics.Abstractions"
  "src/Granit.Entities.Abstractions"
  "tests/Granit.Analytics.Abstractions.Tests"
  "tests/Granit.Entities.Abstractions.Tests"
)

is_retained() {
  local p="$1"
  for r in "${RETAIN[@]}"; do
    [[ "$p" == "$r" ]] && return 0
  done
  return 1
}

collect() {
  local -a out=()
  for root in "${ROOTS[@]}"; do
    # src/ projects: Granit.X or Granit.X.Anything
    while IFS= read -r d; do
      [[ -z "$d" ]] && continue
      is_retained "$d" && continue
      out+=("$d")
    done < <(find src -maxdepth 1 -mindepth 1 -type d \
              \( -name "$root" -o -name "$root.*" \) \
              -printf '%p\n' 2>/dev/null | sort)

    # tests/ projects: Granit.X.Tests, Granit.X.*.Tests, Granit.X.*.Tests.Integration
    while IFS= read -r d; do
      [[ -z "$d" ]] && continue
      is_retained "$d" && continue
      out+=("$d")
    done < <(find tests -maxdepth 1 -mindepth 1 -type d \
              \( -name "$root.Tests" \
                 -o -name "$root.Tests.Integration" \
                 -o -name "$root.*.Tests" \
                 -o -name "$root.*.Tests.Integration" \) \
              -printf '%p\n' 2>/dev/null | sort)
  done

  # Bundles that are 100% business.
  for b in src/bundles/Granit.Bundle.Documents src/bundles/Granit.Bundle.SaaS; do
    [[ -d "$b" ]] && out+=("$b")
  done

  # Observability satellites — framework modules' metric/dashboard satellites
  # whose runtime targets the business analytics/dashboards stack. They depend
  # on the framework module via NuGet once extracted.
  for sat in \
    src/Granit.BlobStorage.Analytics \
    src/Granit.BlobStorage.Dashboards \
    src/Granit.Webhooks.Analytics \
    src/Granit.Webhooks.Dashboards \
    src/Granit.Notifications.Analytics \
    src/Granit.Identity.Federated.Analytics \
    tests/Granit.BlobStorage.Analytics.Tests \
    tests/Granit.BlobStorage.Dashboards.Tests \
    tests/Granit.Webhooks.Analytics.Tests \
    tests/Granit.Webhooks.Dashboards.Tests \
    tests/Granit.Notifications.Analytics.Tests \
    tests/Granit.Identity.Federated.Analytics.Tests; do
    [[ -d "$sat" ]] && out+=("$sat")
  done

  printf '%s\n' "${out[@]}"
}

mode="${1:-plain}"
case "$mode" in
  plain)
    collect
    ;;
  --filter-repo)
    collect | sed 's/^/--path /'
    ;;
  --paths-file)
    collect
    ;;
  --count)
    collect | wc -l
    ;;
  *)
    echo "Unknown mode: $mode" >&2
    echo "Usage: $0 [plain|--filter-repo|--paths-file|--count]" >&2
    exit 2
    ;;
esac
