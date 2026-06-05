using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;

namespace Granit.DataLookup.EntityFrameworkCore;

/// <summary>
/// Granit module for the Entity Framework Core adapter of Granit.DataLookup.
/// </summary>
/// <remarks>
/// Provides <see cref="Sources.QueryableLookupSource{T}"/> and
/// <see cref="Sources.QueryDefinitionLookupSource{T}"/>. Module authors register sources with
/// <see cref="Extensions.DataLookupQueryableExtensions.AddQueryableLookup{T, TDbContext}"/> or
/// <see cref="Extensions.QueryDefinitionLookupExtensions.AddQueryDefinitionLookup{TEntity, TDbContext}"/>.
/// </remarks>
[DependsOn(
    typeof(GranitDataLookupModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitDataLookupEntityFrameworkCoreModule : GranitModule;
