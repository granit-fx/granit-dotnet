namespace Granit.DataExchange.Export;

/// <summary>
/// Provides additional export fields for entities with dynamic properties
/// (e.g. <c>IHasMetadata</c> with mapped extra properties).
/// </summary>
/// <remarks>
/// The default implementation returns an empty list. The EF Core layer
/// registers an implementation that reads from <c>IMetadataMappingRegistry</c>.
/// </remarks>
public interface IExtraExportFieldProvider
{
    /// <summary>
    /// Gets extra field descriptors for the specified entity type.
    /// </summary>
    /// <param name="entityType">The CLR entity type.</param>
    /// <returns>
    /// A list of field descriptors for mapped extra properties, or an empty list
    /// if the entity has no mapped extra properties.
    /// </returns>
    IReadOnlyList<ExportFieldDescriptor> GetExtraFields(Type entityType);
}
