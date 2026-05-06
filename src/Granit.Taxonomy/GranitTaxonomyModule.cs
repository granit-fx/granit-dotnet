using Granit.Modularity;
using Granit.Persistence;

namespace Granit.Taxonomy;

/// <summary>
/// Granit module for cross-cutting taxonomy: scoped tags and hierarchical categories
/// assignable to any aggregate root.
/// </summary>
/// <remarks>
/// <para>
/// One shared <c>Tag</c> table carries a <c>Scope</c> discriminator so each consumer
/// module gets a focused autocomplete while cross-entity search runs as a single query.
/// Hosts that prefer a single global pot collapse all scopes to <c>"global"</c>.
/// </para>
/// <para>
/// Phase T1 ships only the package skeleton. Domain types (<c>Tag</c>, <c>TagAssignment</c>,
/// <c>Category</c>) and persistence are introduced in story T1.2;
/// endpoints in T2. See ADR-054 for the full phasing plan.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitPersistenceModule))]
public sealed class GranitTaxonomyModule : GranitModule;
