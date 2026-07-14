namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Default <see cref="IImportEntityBinding"/> closing over the entity type of a registered
/// <see cref="ImportDefinition{TEntity}"/> so visitors can recover it without reflection.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
internal sealed class ImportEntityBinding<TEntity>(ImportDefinition<TEntity> definition) : IImportEntityBinding
    where TEntity : class
{
    /// <inheritdoc/>
    public IImportDefinitionDescriptor Descriptor => definition;

    /// <inheritdoc/>
    public TResult Accept<TResult>(IImportEntityVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        return visitor.Visit(definition);
    }
}
