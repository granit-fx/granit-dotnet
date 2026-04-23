using Granit.DataLookup;
using Granit.Modularity;

namespace Granit.DataLookup.EntityFrameworkCore;

/// <summary>
/// Granit module for the Entity Framework Core adapter of Granit.DataLookup.
/// </summary>
/// <remarks>
/// Provides <see cref="Sources.QueryableLookupSource{T}"/>. Module authors register
/// sources with the <see cref="Extensions.DataLookupQueryableExtensions.AddQueryableLookup{T}"/>
/// extension.
/// </remarks>
[DependsOn(typeof(GranitDataLookupModule))]
public sealed class GranitDataLookupEntityFrameworkCoreModule : GranitModule;
