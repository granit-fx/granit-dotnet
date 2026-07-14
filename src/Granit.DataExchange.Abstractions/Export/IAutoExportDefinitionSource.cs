namespace Granit.DataExchange.Export;

/// <summary>
/// Provides entity types eligible for auto-generated export definitions.
/// </summary>
/// <remarks>
/// <para>
/// Implementations discover entity types at runtime — typically by scanning
/// EF Core <c>DbContext</c> models. The export pipeline registry creates
/// <c>ReflectionExportDefinition</c> instances for each type
/// that does not already have an explicit <see cref="ExportDefinition{TEntity}"/>.
/// </para>
/// <para>
/// This interface is defined in <c>Granit.DataExchange</c> (no EF Core dependency).
/// The EF Core implementation lives in <c>Granit.DataExchange.EntityFrameworkCore</c>.
/// </para>
/// </remarks>
public interface IAutoExportDefinitionSource
{
    /// <summary>
    /// Returns the CLR types of all entities eligible for auto-generated export.
    /// </summary>
    IReadOnlyList<Type> GetEntityTypes();
}
