namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Default <see cref="IExportEntityBinding"/> implementation binding an
/// <see cref="ExportDefinition{TEntity}"/> to its compile-time entity type.
/// </summary>
/// <typeparam name="TEntity">The source entity type of the definition.</typeparam>
internal sealed class ExportEntityBinding<TEntity>(ExportDefinition<TEntity> definition) : IExportEntityBinding
    where TEntity : class
{
    /// <inheritdoc/>
    public IExportDefinitionDescriptor Descriptor => definition;

    /// <inheritdoc/>
    public TResult Accept<TResult>(IExportEntityVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        return visitor.Visit(definition);
    }
}
