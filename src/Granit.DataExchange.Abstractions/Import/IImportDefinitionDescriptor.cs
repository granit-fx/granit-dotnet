namespace Granit.DataExchange.Import;

/// <summary>
/// Non-generic view of an <see cref="ImportDefinition{TEntity}"/> for runtime resolution by name.
/// </summary>
/// <remarks>
/// Registered as a singleton alongside the generic <see cref="ImportDefinition{TEntity}"/> by
/// <see cref="Extensions.ImportDefinitionServiceCollectionExtensions.AddImportDefinition{TEntity,TDefinition}"/>.
/// Endpoints and other services can enumerate <c>IEnumerable&lt;IImportDefinitionDescriptor&gt;</c>
/// to find a definition by name without compile-time knowledge of the entity type.
/// </remarks>
public interface IImportDefinitionDescriptor
{
    /// <summary>
    /// The definition name (e.g. <c>"Acme.PatientImport"</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The target entity CLR type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Maximum allowed file size in megabytes.
    /// </summary>
    int MaxFileSizeMb { get; }

    /// <summary>
    /// Allowed MIME types for upload.
    /// </summary>
    IReadOnlyList<string> AllowedMimeTypes { get; }

    /// <summary>
    /// Gets field metadata for all declared properties (for the mapping suggestion pipeline).
    /// </summary>
    IReadOnlyList<ImportFieldMetadata> GetFieldMetadata();
}
