using Granit.Caching;
using Granit.Core.Modularity;
using Granit.Persistence;

namespace Granit.ReferenceData.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of reference data.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-register a store — the host application must call
/// <c>services.AddReferenceDataStore&lt;TEntity, TDbContext&gt;()</c> for each
/// reference data entity type.
/// </para>
/// <para>
/// The host DbContext must call <c>modelBuilder.ConfigureReferenceData&lt;TEntity&gt;()</c>
/// in <c>OnModelCreating</c> for each reference data entity type.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitCachingModule),
    typeof(GranitPersistenceModule),
    typeof(GranitReferenceDataModule))]
public sealed class GranitReferenceDataEntityFrameworkCoreModule : GranitModule;
