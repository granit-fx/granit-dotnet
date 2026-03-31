#!/usr/bin/env python3
"""Generate domain-based solution filter (.slnf) files for IDE use.

Generates one filter per .slnx domain folder (Platform, Security, etc.)
plus a special framework-only filter. Each filter includes domain src
projects, their transitive dependencies, and matching test projects.

No external dependencies -- runs with Python 3.8+.

Usage (from granit-dotnet repo root):
    python3 scripts/generate-domain-filters.py
"""

from __future__ import annotations

import sys
from pathlib import Path

from _slnx_utils import (
    REPO_ROOT,
    SLNX_PATH,
    load_slnx_projects,
    generate_slnf,
    write_slnf,
)
from _domain_map import DOMAIN_ORDER, classify_src, classify_test

FILTERS_DIR = REPO_ROOT / ".slnf"
SOLUTION_PATH = "Granit.slnx"

# Module roots for framework-only filter.
# Authoritative source: FrameworkBoundaryRules.ModuleRootPrefixes (C#).
MODULE_ROOTS: frozenset[str] = frozenset({
    "Auditing", "BackgroundJobs", "Bff", "BlobStorage",
    "DataExchange", "DocumentGeneration", "Http.Cookies",
    "Identity", "Imaging", "Notifications", "Oidc",
    "OpenIddict", "ReferenceData", "Templating",
    "Timeline", "Webhooks", "Workflow",
})


def is_module_project(project_name: str) -> bool:
    """Return True if the project is a module (not framework)."""
    if not project_name.startswith("Granit."):
        return False
    suffix = project_name[len("Granit."):]
    for root in MODULE_ROOTS:
        if suffix == root or suffix.startswith(root + "."):
            return True
    return False


def classify_all_projects(
    slnx_projects: set[str],
) -> tuple[dict[str, list[str]], dict[str, list[str]]]:
    """Classify all slnx projects into src and test domain buckets."""
    src_by_domain: dict[str, list[str]] = {d: [] for d in DOMAIN_ORDER}
    src_by_domain["Bundles"] = []
    test_by_domain: dict[str, list[str]] = {d: [] for d in DOMAIN_ORDER}
    test_by_domain["Architecture"] = []

    for path in sorted(slnx_projects):
        if path.startswith("src/"):
            domain = classify_src(path)
            if domain in src_by_domain:
                src_by_domain[domain].append(path)
        elif path.startswith("tests/"):
            domain = classify_test(path)
            if domain in test_by_domain:
                test_by_domain[domain].append(path)

    return src_by_domain, test_by_domain


def generate_framework_filter(
    slnx_projects: set[str],
) -> list[str]:
    """Collect all framework-only project paths (src + tests, excluding modules)."""
    framework_src = [
        p for p in slnx_projects
        if p.startswith("src/")
        and "bundles/" not in p
        and not is_module_project(Path(p).stem)
    ]
    fw_test_paths: list[str] = []
    for p in sorted(slnx_projects):
        if not p.startswith("tests/"):
            continue
        if "ArchitectureTests" in p:
            continue
        base_name = Path(p).stem.replace(".Tests.Integration", "").replace(".Tests", "")
        if not is_module_project(base_name):
            fw_test_paths.append(p)

    return framework_src + fw_test_paths


def main() -> None:
    if not SLNX_PATH.is_file():
        print(f"ERROR: {SLNX_PATH} not found. Run from repo root.", file=sys.stderr)
        sys.exit(1)

    slnx_projects = load_slnx_projects()
    print(f"Loaded {len(slnx_projects)} projects from {SLNX_PATH.name}")
    FILTERS_DIR.mkdir(parents=True, exist_ok=True)

    src_by_domain, test_by_domain = classify_all_projects(slnx_projects)

    # -- Per-domain filters -------------------------------------------------
    for domain in DOMAIN_ORDER:
        name = domain.lower()
        domain_projects = src_by_domain.get(domain, []) + test_by_domain.get(domain, [])
        if not domain_projects:
            continue

        print(f"Generating {name}.slnf ({len(domain_projects)} domain projects)...")
        slnf = generate_slnf(domain_projects, slnx_projects, SOLUTION_PATH)
        out = FILTERS_DIR / f"{name}.slnf"
        write_slnf(out, slnf)
        print(f"  -> {out.relative_to(REPO_ROOT)} ({len(slnf['solution']['projects'])} projects)")

    # -- Framework-only filter ----------------------------------------------
    all_fw = generate_framework_filter(slnx_projects)
    print(f"Generating framework-only.slnf ({len(all_fw)} framework projects)...")
    slnf = generate_slnf(all_fw, slnx_projects, SOLUTION_PATH)
    out = FILTERS_DIR / "framework-only.slnf"
    write_slnf(out, slnf)
    print(f"  -> {out.relative_to(REPO_ROOT)} ({len(slnf['solution']['projects'])} projects)")

    total = len(DOMAIN_ORDER) + 1
    print(f"\nDone. Generated {total} solution filters in {FILTERS_DIR.relative_to(REPO_ROOT)}/")


if __name__ == "__main__":
    main()
