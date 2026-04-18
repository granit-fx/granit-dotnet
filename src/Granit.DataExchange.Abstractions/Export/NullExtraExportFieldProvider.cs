namespace Granit.DataExchange.Export;

/// <summary>
/// Default <see cref="IExtraExportFieldProvider"/> that returns no extra fields.
/// Replaced by the EF Core implementation when <c>Granit.DataExchange.EntityFrameworkCore</c> is loaded.
/// </summary>
internal sealed class NullExtraExportFieldProvider : IExtraExportFieldProvider
{
    public IReadOnlyList<ExportFieldDescriptor> GetExtraFields(Type entityType) => [];
}
