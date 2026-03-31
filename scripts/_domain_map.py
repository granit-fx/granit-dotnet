"""Domain classification for Granit solution projects.

Internal module — imported by reorganize-slnx.py and generate-domain-filters.py.
Not intended for standalone use.

Maps each src/ or tests/ project to a functional domain (Platform, Security, etc.)
based on its module family (the second segment of the project name after "Granit.").
"""

from __future__ import annotations

import re

# ---------------------------------------------------------------------------
# Module-family → domain mapping
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
    "Compliance": {"Auditing", "Privacy"},
    "Notifications": {"Notifications"},
    "AI": {"AI"},
    "Tooling": {"Analyzers", "Testing"},
}

# Special cases: projects classified by full directory name, not just module family.
# Http.Cookies is Compliance (cookie consent / GDPR), not Platform.
SPECIAL_CASES: dict[str, str] = {
    "Granit.Http.Cookies": "Compliance",
    "Granit.Http.Cookies.Endpoints": "Compliance",
    "Granit.Http.Cookies.Klaro": "Compliance",
}

# Build reverse lookup: module_family → domain
_MODULE_TO_DOMAIN: dict[str, str] = {}
for _domain, _modules in SRC_DOMAINS.items():
    for _m in _modules:
        _MODULE_TO_DOMAIN[_m] = _domain

# Ordered domain list (determines output order)
DOMAIN_ORDER = [
    "Platform", "Security", "Application", "Infrastructure",
    "Compliance", "Notifications", "AI", "Tooling",
]


def classify_src(path: str) -> str:
    """Classify a src/ project path into a domain."""
    # Bundles handled separately
    if "bundles/" in path:
        return "Bundles"

    # Extract directory name: src/Granit.Foo.Bar/... → Granit.Foo.Bar
    parts = path.split("/")
    if len(parts) < 2:
        return "Platform"
    proj_dir = parts[1]

    # Check special cases first (e.g., Http.Cookies → Compliance)
    if proj_dir in SPECIAL_CASES:
        return SPECIAL_CASES[proj_dir]

    # Base package "Granit" (was Granit.Core) → Platform
    if proj_dir == "Granit":
        return "Platform"

    segments = proj_dir.split(".")
    if len(segments) < 2:
        return "Platform"

    module_family = segments[1]  # "AI", "BlobStorage", "Http", etc.
    return _MODULE_TO_DOMAIN.get(module_family, "Platform")


def classify_test(path: str) -> str:
    """Classify a tests/ project path into a domain."""
    parts = path.split("/")
    if len(parts) < 2:
        return "Platform"
    proj_dir = parts[1]

    # ArchitectureTests → dedicated folder
    if proj_dir.startswith("Granit.ArchitectureTests"):
        return "Architecture"

    # Bundle tests → Platform
    if "Bundle" in proj_dir:
        return "Platform"

    # Remove .Tests / .Tests.Integration suffix to find source module
    base = re.sub(r"\.Tests(\.Integration)?$", "", proj_dir)
    return classify_src(f"src/{base}/{base}.csproj")
