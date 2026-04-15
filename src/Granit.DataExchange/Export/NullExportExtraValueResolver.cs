using Granit.Domain;

namespace Granit.DataExchange.Export;

/// <summary>
/// Default <see cref="IExportExtraValueResolver"/> that reads from the JSON property bag.
/// Only returns values for <b>unmapped</b> extra properties (mapped properties are stored
/// in shadow columns and require the EF Core implementation).
/// </summary>
internal sealed class NullExportExtraValueResolver : IExportExtraValueResolver
{
    public object? ResolveExtraValue(object entity, string propertyName)
    {
        if (entity is IHasExtraProperties hasExtra)
        {
            return hasExtra.GetExtraProperty(propertyName);
        }

        return null;
    }
}
