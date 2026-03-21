#!/usr/bin/env python3
"""Generate solution filter (.slnf) files for CI.

Generates two types of filters:
- Per-shard filters for test jobs (from .github/test-shards.json)
- A src-only filter for the build gate job (all src/ projects, no tests)

No external dependencies — runs with Python 3.8+.

Usage (from granit-dotnet repo root):
    python3 scripts/generate-shard-filters.py
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SHARDS_PATH = REPO_ROOT / ".github" / "test-shards.json"
FILTERS_DIR = REPO_ROOT / ".github" / "shard-filters"
SLNX_PATH = REPO_ROOT / "Granit.slnx"

# Analyzers are implicitly referenced by Directory.Build.props for every project.
# They must be included in every shard filter so MSBuild can resolve them.
IMPLICIT_PROJECTS = [
    "src/Granit.Analyzers/Granit.Analyzers.csproj",
    "src/Granit.Analyzers.CodeFixes/Granit.Analyzers.CodeFixes.csproj",
]

# Regex to extract ProjectReference Include paths from .csproj XML.
# Matches both forward-slash and backslash separators.
PROJECT_REF_RE = re.compile(
    r'<ProjectReference\s+Include="([^"]+\.csproj)"', re.IGNORECASE
)

# Regex to extract project paths from .slnx (<Project Path="..." />)
SLNX_PROJECT_RE = re.compile(r'<Project\s+Path="([^"]+\.csproj)"', re.IGNORECASE)


def load_slnx_projects() -> set[str]:
    """Load all project paths from the .slnx solution file.

    Returns a set of forward-slash relative paths (e.g. 'src/Granit.Core/Granit.Core.csproj').
    """
    text = SLNX_PATH.read_text(encoding="utf-8")
    return set(SLNX_PROJECT_RE.findall(text))


def resolve_csproj(project_dir: str) -> Path | None:
    """Find the .csproj file in a project directory."""
    d = REPO_ROOT / project_dir
    if not d.is_dir():
        return None
    name = d.name + ".csproj"
    csproj = d / name
    return csproj if csproj.is_file() else None


def parse_project_references(csproj: Path) -> list[Path]:
    """Extract ProjectReference paths from a .csproj file, resolved to absolute."""
    text = csproj.read_text(encoding="utf-8")
    refs: list[Path] = []
    for match in PROJECT_REF_RE.findall(text):
        # Normalize backslashes and resolve relative to the .csproj directory
        rel = match.replace("\\", "/")
        # Skip MSBuild property references like $(MSBuildThisFileDirectory)
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
    """Convert an absolute .csproj path to a path relative to the repo root,
    using forward slashes (as used in .slnx)."""
    return str(csproj.relative_to(REPO_ROOT)).replace("\\", "/")


def generate_filter(
    shard_name: str, project_dirs: list[str], slnx_projects: set[str]
) -> dict:
    """Generate a .slnf structure for a shard."""
    all_projects: set[Path] = set()

    for project_dir in project_dirs:
        csproj = resolve_csproj(project_dir)
        if csproj is None:
            print(f"  WARNING: no .csproj found in {project_dir}, skipping", file=sys.stderr)
            continue
        # Collect the test project and all its transitive deps
        collect_transitive_deps(csproj, all_projects)

    # Add implicit analyzer projects
    for implicit in IMPLICIT_PROJECTS:
        p = (REPO_ROOT / implicit).resolve()
        if p.is_file():
            all_projects.add(p)

    # Convert to slnx-relative paths and filter to only include projects
    # that are actually listed in the .slnx solution file.
    paths: list[str] = []
    excluded = 0
    for p in all_projects:
        slnx_path = to_slnx_path(p)
        if slnx_path in slnx_projects:
            paths.append(slnx_path)
        else:
            excluded += 1
            print(f"  INFO: {slnx_path} not in .slnx, excluded from filter", file=sys.stderr)

    paths.sort()

    return {
        "solution": {
            "path": "../../Granit.slnx",
            "projects": paths,
        }
    }


def main() -> None:
    if not SHARDS_PATH.is_file():
        print(f"ERROR: {SHARDS_PATH} not found. Run from repo root.", file=sys.stderr)
        sys.exit(1)

    if not SLNX_PATH.is_file():
        print(f"ERROR: {SLNX_PATH} not found.", file=sys.stderr)
        sys.exit(1)

    shards = json.loads(SHARDS_PATH.read_text(encoding="utf-8"))["shards"]
    slnx_projects = load_slnx_projects()
    print(f"Loaded {len(slnx_projects)} projects from {SLNX_PATH.name}")
    FILTERS_DIR.mkdir(parents=True, exist_ok=True)

    # ── Shard filters (test jobs) ───────────────────────────────────────────
    for shard in shards:
        name = shard["name"]
        projects = shard["projects"]
        print(f"Generating {name}.slnf ({len(projects)} test projects)...")
        slnf = generate_filter(name, projects, slnx_projects)
        out = FILTERS_DIR / f"{name}.slnf"
        out.write_text(
            json.dumps(slnf, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
        print(f"  -> {out.relative_to(REPO_ROOT)} ({len(slnf['solution']['projects'])} projects)")

    # ── src-only filter (build gate job) ──────────────────────────────────
    src_projects = sorted(p for p in slnx_projects if p.startswith("src/"))
    src_slnf = {
        "solution": {
            "path": "../../Granit.slnx",
            "projects": src_projects,
        }
    }
    src_out = FILTERS_DIR / "src-only.slnf"
    src_out.write_text(
        json.dumps(src_slnf, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print(f"Generating src-only.slnf...")
    print(f"  -> {src_out.relative_to(REPO_ROOT)} ({len(src_projects)} projects)")

    total = len(shards) + 1
    print(f"\nDone. Generated {total} solution filters in {FILTERS_DIR.relative_to(REPO_ROOT)}/")


if __name__ == "__main__":
    main()
