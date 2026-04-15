using Granit.DataExchange.Export;
using Granit.Persistence.EntityFrameworkCore.ExtraProperties;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export;

/// <summary>
/// <see cref="IExtraExportFieldProvider"/> backed by <see cref="IExtraPropertyMappingRegistry"/>.
/// Returns one <see cref="ExportFieldDescriptor"/> per mapped extra property.
/// </summary>
internal sealed class EfCoreExtraExportFieldProvider(
    IExtraPropertyMappingRegistry registry) : IExtraExportFieldProvider
{
    public IReadOnlyList<ExportFieldDescriptor> GetExtraFields(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        IReadOnlyList<ExtraPropertyMapping> mappings = registry.GetMappings(entityType);
        if (mappings.Count == 0)
        {
            return [];
        }

        List<ExportFieldDescriptor> fields = new(mappings.Count);
        for (int i = 0; i < mappings.Count; i++)
        {
            ExtraPropertyMapping mapping = mappings[i];
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
