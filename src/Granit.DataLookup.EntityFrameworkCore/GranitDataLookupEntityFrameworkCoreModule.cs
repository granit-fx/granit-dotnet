using Granit.DataLookup;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.DataLookup.EntityFrameworkCore;

/// <summary>
/// Granit module for the Entity Framework Core adapter of Granit.DataLookup.
/// </summary>
/// <remarks>
/// Provides <see cref="Sources.QueryableLookupSource{T}"/>. Module authors register
/// sources with the <see cref="Extensions.DataLookupQueryableExtensions.AddQueryableLookup{T, TDbContext}"/>
/// extension.
/// </remarks>
[DependsOn(
    typeof(GranitDataLookupModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitDataLookupEntityFrameworkCoreModule : GranitModule;
