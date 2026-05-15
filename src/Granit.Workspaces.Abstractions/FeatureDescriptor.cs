namespace Granit.Workspaces;

/// <summary>
/// Immutable description of a navigable feature exposed by a module
/// (per ADR-057). A feature is a *capability* — a thing a user can do,
/// gated by one permission, rendered behind one logical frontend route.
/// Modules declare features via <see cref="IFeatureProvider"/>; hosts
/// compose workspaces by referencing features through
/// <c>WorkspaceSectionBuilder.Feature(name)</c>.
/// </summary>
/// <param name="Name">
/// Globally unique wire identifier. Convention:
/// <c>{module}.{entity-plural}.{view}</c> (kebab-case segments, dot-separated)
/// e.g. <c>"invoicing.invoices.list"</c>, <c>"identity.users.detail"</c>.
/// The composer rejects duplicates at boot.
/// </param>
/// <param name="Permission">
/// Permission gate — the feature is filtered out of the workspace tree for
/// users who don't hold it (defense in depth, ADR-040 §6). Must be a
/// permission declared by some <c>IPermissionDefinitionProvider</c>.
/// </param>
/// <param name="RouteName">
/// Logical frontend route identifier (per ADR-057 §5). The host's React
/// route table maps this to an actual SPA path. Defaults to
/// <see cref="Name"/> when convention holds; explicit override permits
/// reusing the same feature on multiple paths (e.g. tenant vs. host shell).
/// </param>
/// <param name="DefaultIcon">
/// Lucide icon name. Per-placement overrides on the workspace item win.
/// </param>
/// <param name="DisplayKey">
/// Localisation key for the feature label (e.g.
/// <c>"InvoicingEndpoints:Invoices.List"</c>). Mandatory in all 18 cultures
/// (CLAUDE.md localisation rules).
/// </param>
public sealed record FeatureDescriptor(
    string Name,
    string Permission,
    string RouteName,
    string? DefaultIcon,
    string DisplayKey);
