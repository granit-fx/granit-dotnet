using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Mapping;

namespace Granit.DataExchange.Endpoints.Internal.Import;

/// <summary>
/// Thin delegate to <see cref="Granit.DataExchange.Import.Internal.ImportDefinitionResolver"/>.
/// Kept for endpoint-local usages that haven't migrated yet (e.g. export endpoints).
/// </summary>
internal static class ImportDefinitionResolver
{
    internal static IImportDefinitionDescriptor? FindByName(
        IServiceProvider serviceProvider,
        string definitionName) =>
        Granit.DataExchange.Import.Internal.ImportDefinitionResolver.FindByName(serviceProvider, definitionName);

    internal static Task<IReadOnlyList<ImportColumnMapping>> SuggestMappingsAsync(
        IMappingSuggestionService mappingService,
        Type entityType,
        IReadOnlyList<string> headers,
        CancellationToken cancellationToken) =>
        Granit.DataExchange.Import.Internal.ImportDefinitionResolver.SuggestMappingsAsync(
            mappingService, entityType, headers, cancellationToken);
}
