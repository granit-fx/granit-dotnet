"""Shared utilities for solution and .csproj parsing.

Internal module — imported by generate-shard-filters.py, generate-domain-filters.py,
and reorganize-slnx.py. Not intended for standalone use.

No external dependencies — runs with Python 3.8+.
"""

from __future__ import annotations

import glob
import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
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
    """Find the .csproj file in a project directory (convention: dir_name.csproj)."""
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
        if "*" in rel:
            # MSBuild wildcard ProjectReference (e.g. Granit.*.Endpoints/Granit.*.Endpoints.csproj).
            # glob matches MSBuild's '*' semantics (no separator crossing) and resolves the '..'.
            for matched in sorted(glob.glob(str(csproj.parent / rel))):
                resolved = Path(matched).resolve()
                if resolved.is_file() and resolved not in refs:
                    refs.append(resolved)
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


def generate_slnf(
    csproj_paths: list[str],
    slnx_projects: set[str],
    solution_path: str,
) -> dict:
    """Generate a .slnf structure from slnx-relative .csproj paths.

    Collects transitive dependencies and implicit projects, filters
    to projects present in the .slnx, and returns the filter dict.
    """
    all_projects: set[Path] = set()

    for rel_path in csproj_paths:
        csproj = (REPO_ROOT / rel_path).resolve()
        if not csproj.is_file():
            print(f"  WARNING: {rel_path} not found, skipping", file=sys.stderr)
            continue
        collect_transitive_deps(csproj, all_projects)

    # Add implicit analyzer projects
    for implicit in IMPLICIT_PROJECTS:
        p = (REPO_ROOT / implicit).resolve()
        if p.is_file():
            all_projects.add(p)

    # Convert to slnx-relative paths and filter to only include projects
    # that are actually listed in the .slnx solution file.
    paths: list[str] = sorted(
        to_slnx_path(p) for p in all_projects
        if to_slnx_path(p) in slnx_projects
    )

    return {
        "solution": {
            "path": solution_path,
            "projects": paths,
        }
    }


def write_slnf(path: Path, slnf: dict) -> None:
    """Write a .slnf JSON file."""
    path.write_text(
        json.dumps(slnf, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
