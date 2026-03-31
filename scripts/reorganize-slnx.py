#!/usr/bin/env python3
"""Reorganize Granit.slnx with nested solution folders by domain."""

import re

from _slnx_utils import SLNX_PATH
from _domain_map import DOMAIN_ORDER, classify_src, classify_test

DOMAIN_LABELS = {
    "Platform": "Platform",
    "Security": "Security",
    "Application": "Application",
    "Infrastructure": "Infrastructure",
    "Compliance": "Compliance",
    "Notifications": "Notifications",
    "AI": "AI",
    "Tooling": "Tooling",
}

FOLDER_CLOSE = "  </Folder>"


def parse_projects(content: str) -> tuple[list[str], list[str]]:
    """Extract project paths from .slnx content, separated by src/tests."""
    src_projects = []
    test_projects = []
    for m in re.finditer(r'<Project\s+Path="([^"]+)"', content):
        path = m.group(1)
        if path.startswith("tests/"):
            test_projects.append(path)
        else:
            src_projects.append(path)
    return src_projects, test_projects


def _emit_folder(lines: list[str], name: str, projects: list[str]) -> None:
    """Append a <Folder> element with its projects to the output lines."""
    lines.append(f'  <Folder Name="{name}">')
    for p in projects:
        lines.append(f'    <Project Path="{p}" />')
    lines.append(FOLDER_CLOSE)


def build_slnx(src_projects: list[str], test_projects: list[str]) -> str:
    """Generate the new .slnx content with nested solution folders."""
    # Categorize src projects
    src_by_domain: dict[str, list[str]] = {d: [] for d in DOMAIN_ORDER}
    src_by_domain["Bundles"] = []
    for p in src_projects:
        domain = classify_src(p)
        src_by_domain[domain].append(p)

    # Categorize test projects
    test_domains = DOMAIN_ORDER + ["Architecture"]
    test_by_domain: dict[str, list[str]] = {d: [] for d in test_domains}
    for p in test_projects:
        domain = classify_test(p)
        test_by_domain[domain].append(p)

    # Sort within each domain
    for domain in src_by_domain:
        src_by_domain[domain].sort()
    for domain in test_by_domain:
        test_by_domain[domain].sort()

    lines = [
        "<Solution>",
        "  <Configurations>",
        '    <Platform Name="Any CPU" />',
        '    <Platform Name="x64" />',
        '    <Platform Name="x86" />',
        "  </Configurations>",
    ]

    # .slnx uses FLAT sibling <Folder> elements — hierarchy comes from
    # the Name path, NOT from XML nesting.

    # --- src domain folders ---
    for domain in DOMAIN_ORDER:
        projects = src_by_domain[domain]
        if projects:
            _emit_folder(lines, f"/src/{DOMAIN_LABELS[domain]}/", projects)

    # --- bundles ---
    if src_by_domain["Bundles"]:
        _emit_folder(lines, "/src/Bundles/", src_by_domain["Bundles"])

    # --- test domain folders ---
    test_domain_labels = {
        **DOMAIN_LABELS,
        "Architecture": "Architecture Tests",
    }
    for domain in test_domains:
        projects = test_by_domain[domain]
        if projects:
            _emit_folder(lines, f"/tests/{test_domain_labels[domain]}/", projects)

    lines.append("</Solution>")
    return "\n".join(lines) + "\n"


def main() -> None:
    content = SLNX_PATH.read_text()
    src_projects, test_projects = parse_projects(content)

    print(f"Found {len(src_projects)} src projects, {len(test_projects)} test projects")

    new_content = build_slnx(src_projects, test_projects)
    SLNX_PATH.write_text(new_content)

    # Print summary
    src_by_domain: dict[str, int] = {}
    for p in src_projects:
        d = classify_src(p)
        src_by_domain[d] = src_by_domain.get(d, 0) + 1
    test_by_domain: dict[str, int] = {}
    for p in test_projects:
        d = classify_test(p)
        test_by_domain[d] = test_by_domain.get(d, 0) + 1

    print("\n--- src/ ---")
    for d in DOMAIN_ORDER + ["Bundles"]:
        print(f"  {d}: {src_by_domain.get(d, 0)} projects")
    print(f"  TOTAL: {len(src_projects)}")

    print("\n--- tests/ ---")
    for d in DOMAIN_ORDER + ["Architecture"]:
        print(f"  {d}: {test_by_domain.get(d, 0)} projects")
    print(f"  TOTAL: {len(test_projects)}")


if __name__ == "__main__":
    main()
