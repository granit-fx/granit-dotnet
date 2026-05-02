using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of <c>Granit.Documents</c>.
/// </summary>
/// <remarks>
/// <para>
/// Provides the isolated <c>DocumentsDbContext</c> and the <c>ConfigureDocumentsModule</c>
/// model-builder extension. The DbContext is registered via the host extension
/// <c>AddGranitDocumentsEntityFrameworkCore(...)</c>; this module class only declares the
/// dependency graph for the Granit module system.
/// </para>
/// <para>
/// Phase-1 scaffolding: the DbContext is empty until aggregate roots arrive in subsequent
/// stories (F2 <c>Folder</c>, F3 <c>Document</c>, …). Migrations are not shipped from the
/// framework package — the consuming application generates them against its own composed
/// model.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitDocumentsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitDocumentsEntityFrameworkCoreModule : GranitModule;
