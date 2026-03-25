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

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SLNX_PATH = REPO_ROOT / "Granit.slnx"
FILTERS_DIR = REPO_ROOT / ".slnf"

# Regex to extract ProjectReference Include paths from .csproj XML.
PROJECT_REF_RE = re.compile(
    r'<ProjectReference\s+Include="([^"]+\.csproj)"', re.IGNORECASE
)

# Regex to extract project paths from .slnx (<Project Path="..." />)
SLNX_PROJECT_RE = re.compile(r'<Project\s+Path="([^"]+\.csproj)"', re.IGNORECASE)

# Analyzers are implicitly referenced by Directory.Build.props for every project.
IMPLICIT_PROJECTS = [
    "src/Granit.Analyzers/Granit.Analyzers.csproj",
    "src/Granit.Analyzers.CodeFixes/Granit.Analyzers.CodeFixes.csproj",
]

# ---------------------------------------------------------------------------
# Domain classification (duplicated from reorganize-slnx.py for standalone use)
# ---------------------------------------------------------------------------
SRC_DOMAINS: dict[str, set[str]] = {
    "Platform": {
        "Diagnostics", "Guids", "Http", "MultiTenancy", "Observability",
        "Oidc", "RateLimiting", "Timing", "Validation",
    },
    "Security": {
        "Authentication", "Authorization", "Bff", "Encryption",
        "Identity", "OpenIddict", "Security", "Vault",
    },
    "Application": {
        "DataExchange", "DocumentGeneration", "Features", "Localization",
        "QueryEngine", "ReferenceData", "Settings", "Templating",
        "Timeline", "Workflow",
    },
    "Infrastructure": {
        "BackgroundJobs", "BlobStorage", "Caching", "Events",
        "Imaging", "Persistence", "Webhooks", "Wolverine",
    },
    "Compliance": {"AuditLog", "Privacy"},
    "Notifications": {"Notifications"},
    "AI": {"AI"},
    "Tooling": {"Analyzers", "Testing"},
}

# Special cases: projects classified by full directory name, not module family.
_SPECIAL_CASES: dict[str, str] = {
    "Granit.Http.Cookies": "Compliance",
    "Granit.Http.Cookies.Endpoints": "Compliance",
    "Granit.Http.Cookies.Klaro": "Compliance",
}

# Build reverse lookup
_MODULE_TO_DOMAIN: dict[str, str] = {}
for _domain, _modules in SRC_DOMAINS.items():
    for _m in _modules:
        _MODULE_TO_DOMAIN[_m] = _domain

DOMAIN_ORDER = [
    "Platform", "Security", "Application", "Infrastructure",
    "Compliance", "Notifications", "AI", "Tooling",
]

# Module roots for framework-only filter.
# Authoritative source: FrameworkBoundaryRules.ModuleRootPrefixes (C#).
MODULE_ROOTS: frozenset[str] = frozenset({
    "AuditLog", "BackgroundJobs", "Bff", "BlobStorage",
    "DataExchange", "DocumentGeneration", "Http.Cookies",
    "Identity", "Imaging", "Notifications", "Oidc",
    "OpenIddict", "ReferenceData", "Templating",
    "Timeline", "Webhooks", "Workflow",
})


# ---------------------------------------------------------------------------
# Classification helpers
# ---------------------------------------------------------------------------

def classify_src(path: str) -> str:
    """Classify a src/ project path into a domain."""
    if "bundles/" in path:
        return "Bundles"

    parts = path.split("/")
    if len(parts) < 2:
        return "Platform"
    proj_dir = parts[1]

    if proj_dir in _SPECIAL_CASES:
        return _SPECIAL_CASES[proj_dir]

    if proj_dir == "Granit":
        return "Platform"

    segments = proj_dir.split(".")
    if len(segments) < 2:
        return "Platform"

    module_family = segments[1]
    return _MODULE_TO_DOMAIN.get(module_family, "Platform")


def is_module_project(project_name: str) -> bool:
    """Return True if the project is a module (not framework)."""
    if not project_name.startswith("Granit."):
        return False
    suffix = project_name[len("Granit."):]
    for root in MODULE_ROOTS:
        if suffix == root or suffix.startswith(root + "."):
            return True
    return False


def classify_test(path: str) -> str:
    """Classify a tests/ project path into a domain."""
    parts = path.split("/")
    if len(parts) < 2:
        return "Platform"
    proj_dir = parts[1]

    if proj_dir.startswith("Granit.ArchitectureTests"):
        return "Architecture"

    if "Bundle" in proj_dir:
        return "Platform"

    # Remove .Tests / .Tests.Integration suffix to find source module
    base = proj_dir.replace(".Tests.Integration", "").replace(".Tests", "")
    return classify_src(f"src/{base}/{base}.csproj")


# ---------------------------------------------------------------------------
# Project reference parsing (same as generate-shard-filters.py)
# ---------------------------------------------------------------------------

def parse_project_references(csproj: Path) -> list[Path]:
    """Extract ProjectReference paths from a .csproj, resolved to absolute."""
    text = csproj.read_text(encoding="utf-8")
    refs: list[Path] = []
    for match in PROJECT_REF_RE.findall(text):
        rel = match.replace("\\", "/")
        if "$(" in rel:
            continue
        resolved = (csproj.parent / rel).resolve()
        if resolved.is_file():
            refs.append(resolved)
    return refs


def collect_transitive_deps(csproj: Path, visited: set[Path] | None = None) -> set[Path]:
    """Recursively collect all transitive ProjectReference dependencies."""
    if visited is None:
        visited = set()
    if csproj in visited:
        return visited
    visited.add(csproj)
    for ref in parse_project_references(csproj):
        collect_transitive_deps(ref, visited)
    return visited


def to_slnx_path(csproj: Path) -> str:
    """Convert absolute .csproj path to repo-relative forward-slash path."""
    return str(csproj.relative_to(REPO_ROOT)).replace("\\", "/")


# ---------------------------------------------------------------------------
# Filter generation
# ---------------------------------------------------------------------------

def load_slnx_projects() -> set[str]:
    """Load all project paths from the .slnx solution file."""
    text = SLNX_PATH.read_text(encoding="utf-8")
    return set(SLNX_PROJECT_RE.findall(text))


def generate_filter(
    project_paths: list[str],
    slnx_projects: set[str],
) -> dict:
    """Generate a .slnf structure from a list of slnx-relative project paths."""
    all_projects: set[Path] = set()

    for rel_path in project_paths:
        csproj = (REPO_ROOT / rel_path).resolve()
        if not csproj.is_file():
            continue
        collect_transitive_deps(csproj, all_projects)

    # Add implicit analyzer projects
    for implicit in IMPLICIT_PROJECTS:
        p = (REPO_ROOT / implicit).resolve()
        if p.is_file():
            all_projects.add(p)

    # Filter to projects in .slnx, sort alphabetically for clean diffs
    paths: list[str] = sorted(
        to_slnx_path(p) for p in all_projects
        if to_slnx_path(p) in slnx_projects
    )

    return {
        "solution": {
            "path": "Granit.slnx",
            "projects": paths,
        }
    }


def write_filter(name: str, slnf: dict) -> None:
    """Write a .slnf file to the filters directory."""
    out = FILTERS_DIR / f"{name}.slnf"
    out.write_text(
        json.dumps(slnf, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    count = len(slnf["solution"]["projects"])
    print(f"  -> {out.relative_to(REPO_ROOT)} ({count} projects)")


def main() -> None:
    if not SLNX_PATH.is_file():
        print(f"ERROR: {SLNX_PATH} not found. Run from repo root.", file=sys.stderr)
        sys.exit(1)

    slnx_projects = load_slnx_projects()
    print(f"Loaded {len(slnx_projects)} projects from {SLNX_PATH.name}")
    FILTERS_DIR.mkdir(parents=True, exist_ok=True)

    # Classify all projects by domain
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

    # -- Per-domain filters -------------------------------------------------
    for domain in DOMAIN_ORDER:
        name = domain.lower()
        domain_projects = src_by_domain.get(domain, []) + test_by_domain.get(domain, [])
        if not domain_projects:
            continue

        print(f"Generating {name}.slnf ({len(domain_projects)} domain projects)...")
        slnf = generate_filter(domain_projects, slnx_projects)
        write_filter(name, slnf)

    # -- Framework-only filter ----------------------------------------------
    framework_src = [
        p for p in slnx_projects
        if p.startswith("src/")
        and "bundles/" not in p
        and not is_module_project(Path(p).stem)
    ]
    framework_tests = [
        p for p in slnx_projects
        if p.startswith("tests/")
        and not any(
            is_module_project(Path(p).stem.replace(".Tests.Integration", "").replace(".Tests", ""))
            for _ in [None]
        )
        and "ArchitectureTests" not in p
    ]

    # Simpler: collect framework src + tests whose base name is framework
    fw_test_paths: list[str] = []
    for p in sorted(slnx_projects):
        if not p.startswith("tests/"):
            continue
        if "ArchitectureTests" in p:
            continue
        base_name = Path(p).stem.replace(".Tests.Integration", "").replace(".Tests", "")
        if not is_module_project(base_name):
            fw_test_paths.append(p)

    all_fw = framework_src + fw_test_paths
    print(f"Generating framework-only.slnf ({len(all_fw)} framework projects)...")
    slnf = generate_filter(all_fw, slnx_projects)
    write_filter("framework-only", slnf)

    total = len(DOMAIN_ORDER) + 1
    print(f"\nDone. Generated {total} solution filters in {FILTERS_DIR.relative_to(REPO_ROOT)}/")


if __name__ == "__main__":
    main()
