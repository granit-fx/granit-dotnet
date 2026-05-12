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
