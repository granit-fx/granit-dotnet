using Granit.DataExchange.Import.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Runtime resolution helpers for import definitions and generic mapping suggestions.
/// </summary>
internal static class ImportDefinitionResolver
{
    /// <summary>
    /// Finds an <see cref="IImportDefinitionDescriptor"/> by name from the registered definitions.
    /// </summary>
    internal static IImportDefinitionDescriptor? FindByName(
        IServiceProvider serviceProvider,
        string definitionName)
    {
        IEnumerable<IImportDefinitionDescriptor> descriptors =
            serviceProvider.GetServices<IImportDefinitionDescriptor>();
        return descriptors.FirstOrDefault(d =>
            string.Equals(d.Name, definitionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Invokes <c>IMappingSuggestionService.SuggestMappingsAsync</c> via reflection,
    /// using the entity type discovered at runtime from the definition descriptor.
    /// </summary>
    internal static async Task<IReadOnlyList<ImportColumnMapping>> SuggestMappingsAsync(
        IMappingSuggestionService mappingService,
        Type entityType,
        IReadOnlyList<string> headers,
        CancellationToken cancellationToken)
    {
        System.Reflection.MethodInfo method = typeof(IMappingSuggestionService)
            .GetMethods()
            .First(m => m.Name == nameof(IMappingSuggestionService.SuggestMappingsAsync)
                         && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        var task =
            (Task<IReadOnlyList<ImportColumnMapping>>)method.Invoke(mappingService, [headers, cancellationToken])!;

        return await task.ConfigureAwait(false);
    }
}
