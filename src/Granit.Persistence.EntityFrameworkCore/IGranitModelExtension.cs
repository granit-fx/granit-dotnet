using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Opt-in hook that lets a separate package augment a Granit module's EF Core model without the module taking
/// a dependency on the augmenting package's technology.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are registered in DI and applied by <see cref="GranitDbContext"/> at the very end of model
/// building — after the module's own <c>OnGranitModelCreating</c> and after <c>ApplyGranitConventions</c>, so
/// an extension gets the final say. The same set of registered extensions is applied to <b>every</b> Granit
/// DbContext in the application, so each implementation MUST guard on the entity types it targets — they only
/// exist in some contexts' models:
/// </para>
/// <code>
/// public void Apply(ModelBuilder modelBuilder, DbContext context)
/// {
///     if (!context.Database.IsNpgsql()) return;                        // provider-gate
///     if (modelBuilder.Model.FindEntityType(typeof(Foo)) is null) return; // entity-gate
///     modelBuilder.Entity&lt;Foo&gt;().Property&lt;Point&gt;("Location")...;
/// }
/// </code>
/// <para>
/// Canonical use: a PostGIS package adds a generated <c>geography(Point)</c> column + GiST index to an
/// address table while NetTopologySuite stays out of the base module. The host owns any migration; this hook
/// only shapes the model.
/// </para>
/// <para>
/// <b>Registration:</b> register as a singleton — the model is built (and cached) once per context type, and
/// the resolved extension set is read from the application service provider at that time. Registering
/// extensions conditionally per request is not supported (the cached model would not reflect later changes).
/// </para>
/// </remarks>
public interface IGranitModelExtension
{
    /// <summary>
    /// Applies model customizations. Called once per model build, after the module's configuration and the
    /// Granit conventions. Implementations must guard on provider and target entity types.
    /// </summary>
    /// <param name="modelBuilder">The model builder for the context being built.</param>
    /// <param name="context">The context being built — exposes the active provider (e.g. <c>context.Database.IsNpgsql()</c>).</param>
    void Apply(ModelBuilder modelBuilder, DbContext context);
}
