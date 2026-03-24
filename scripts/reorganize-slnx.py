#!/usr/bin/env python3
"""Reorganize Granit.slnx with nested solution folders by domain."""

import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SLNX = REPO / "Granit.slnx"

# ---------------------------------------------------------------------------
# Module-family → domain mapping
# ---------------------------------------------------------------------------
SRC_DOMAINS = {
    "Core": {
        "Diagnostics", "Guids", "Observability",
        "Security", "Testing", "Timing", "Validation",
    },
    "Tools": {"Analyzers"},
    "AI": {"AI"},
    "Business": {
        "DataExchange", "DocumentGeneration", "Querying",
        "ReferenceData", "Templating", "Timeline", "Workflow",
    },
    "Http": {
        "Bff", "Http", "RateLimiting", "Webhooks",
    },
    "Data": {
        "BlobStorage", "Caching", "Imaging", "Persistence",
    },
    "Infrastructure": {
        "BackgroundJobs", "EventBus", "Features", "Localization",
        "MultiTenancy", "Notifications", "Settings", "Wolverine",
    },
    "Security": {
        "Authentication", "Authorization", "Encryption",
        "Identity", "OpenIddict", "Vault",
    },
    "Compliance": {"AuditLog", "Privacy"},
}

# Build reverse lookup: module_family → domain
_MODULE_TO_DOMAIN: dict[str, str] = {}
for domain, modules in SRC_DOMAINS.items():
    for m in modules:
        _MODULE_TO_DOMAIN[m] = domain

# Ordered domain list (determines output order)
DOMAIN_ORDER = ["Core", "Tools", "AI", "Business", "Http", "Data", "Infrastructure", "Security", "Compliance"]

DOMAIN_LABELS = {
    "Core": "Core",
    "Tools": "Tools",
    "AI": "AI",
    "Business": "Business",
    "Http": "Http",
    "Data": "Data",
    "Infrastructure": "Infrastructure",
    "Security": "Security",
    "Compliance": "Compliance",
}


def classify_src(path: str) -> str:
    """Classify a src/ project path into a domain."""
    # Bundles handled separately
    if "bundles/" in path:
        return "Bundles"

    # Extract directory name: src/Granit.Foo.Bar/... → Granit.Foo.Bar
    parts = path.split("/")
    if len(parts) < 2:
        return "Core"
    proj_dir = parts[1]  # e.g., "Granit" or "Granit.AI.AzureOpenAI"

    if proj_dir == "Granit":
        return "Core"

    segments = proj_dir.split(".")
    if len(segments) < 2:
        return "Core"

    module_family = segments[1]  # "AI", "BlobStorage", "Http", etc.
    return _MODULE_TO_DOMAIN.get(module_family, "Core")


def classify_test(path: str) -> str:
    """Classify a tests/ project path into a domain."""
    parts = path.split("/")
    if len(parts) < 2:
        return "Core"
    proj_dir = parts[1]  # e.g., "Granit.AI.Tests"

    # ArchitectureTests → dedicated folder
    if proj_dir.startswith("Granit.ArchitectureTests"):
        return "Architecture"

    # Bundle.Tests → Core
    if proj_dir.startswith("Granit.Bundle"):
        return "Core"

    # Granit.Tests (root module tests) → Core
    if proj_dir == "Granit.Tests":
        return "Core"

    segments = proj_dir.split(".")
    if len(segments) < 2:
        return "Core"

    module_family = segments[1]
    return _MODULE_TO_DOMAIN.get(module_family, "Core")


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
        if not projects:
            continue
        label = DOMAIN_LABELS[domain]
        lines.append(f'  <Folder Name="/src/{label}/">')
        for p in projects:
            lines.append(f'    <Project Path="{p}" />')
        lines.append("  </Folder>")

    # --- bundles ---
    bundles = src_by_domain["Bundles"]
    if bundles:
        lines.append('  <Folder Name="/src/Bundles/">')
        for p in bundles:
            lines.append(f'    <Project Path="{p}" />')
        lines.append("  </Folder>")

    # --- test domain folders ---
    test_domain_labels = {
        **DOMAIN_LABELS,
        "Architecture": "Architecture Tests",
    }
    for domain in test_domains:
        projects = test_by_domain[domain]
        if not projects:
            continue
        label = test_domain_labels[domain]
        lines.append(f'  <Folder Name="/tests/{label}/">')
        for p in projects:
            lines.append(f'    <Project Path="{p}" />')
        lines.append("  </Folder>")

    lines.append("</Solution>")
    return "\n".join(lines) + "\n"


def main() -> None:
    content = SLNX.read_text()
    src_projects, test_projects = parse_projects(content)

    print(f"Found {len(src_projects)} src projects, {len(test_projects)} test projects")

    new_content = build_slnx(src_projects, test_projects)
    SLNX.write_text(new_content)

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
