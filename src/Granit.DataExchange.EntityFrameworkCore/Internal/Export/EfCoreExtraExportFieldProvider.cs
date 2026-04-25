using Granit.DataExchange.Export;
using Granit.Persistence.EntityFrameworkCore.Metadata;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export;

/// <summary>
/// <see cref="IExtraExportFieldProvider"/> backed by <see cref="IMetadataMappingRegistry"/>.
/// Returns one <see cref="ExportFieldDescriptor"/> per mapped extra property.
/// </summary>
internal sealed class EfCoreExtraExportFieldProvider(
    IMetadataMappingRegistry registry) : IExtraExportFieldProvider
{
    public IReadOnlyList<ExportFieldDescriptor> GetExtraFields(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        IReadOnlyList<MetadataMapping> mappings = registry.GetMappings(entityType);
        if (mappings.Count == 0)
        {
            return [];
        }

        List<ExportFieldDescriptor> fields = new(mappings.Count);
        for (int i = 0; i < mappings.Count; i++)
        {
            MetadataMapping mapping = mappings[i];
            Type clrType = Nullable.GetUnderlyingType(mapping.ClrType) ?? mapping.ClrType;

            fields.Add(new ExportFieldDescriptor(
                PropertyPath: mapping.Name,
                ClrTypeName: clrType.Name,
                Header: null,
                Format: null,
                Order: 1000 + i,
                IsNavigation: false));
        }

        return fields.AsReadOnly();
    }
}
