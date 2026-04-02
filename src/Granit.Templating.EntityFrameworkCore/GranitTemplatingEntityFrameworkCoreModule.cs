using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Templating.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of template revisions.
/// </summary>
/// <remarks>
/// Registers <c>IDocumentTemplateStoreReader</c> and <c>IDocumentTemplateStoreWriter</c> (scoped).
/// Depends on <see cref="GranitTemplatingModule"/>.
/// <para>
/// Use <c>AddGranitTemplatingEntityFrameworkCore(configure)</c> to register the DbContext.
/// This module does <strong>not</strong> call the extension method — the host application
/// must configure the database provider explicitly.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitTemplatingModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitTemplatingEntityFrameworkCoreModule : GranitModule;
