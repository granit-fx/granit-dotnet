namespace Granit.DataExchange.Import;

/// <summary>
/// Non-generic handle to a registered <see cref="ImportDefinition{TEntity}"/> that preserves
/// the entity type statically via double dispatch.
/// </summary>
/// <remarks>
/// Registered as a singleton alongside the definition by
/// <see cref="Extensions.ImportDefinitionServiceCollectionExtensions.AddImportDefinition{TEntity,TDefinition}"/>.
/// Consumers enumerate <c>IEnumerable&lt;IImportEntityBinding&gt;</c> and call
/// <see cref="Accept{TResult}"/> with an <see cref="IImportEntityVisitor{TResult}"/> to recover
/// the closed generic <see cref="ImportDefinition{TEntity}"/> without reflection.
/// </remarks>
public interface IImportEntityBinding
{
    /// <summary>
    /// The non-generic view of the bound import definition.
    /// </summary>
    IImportDefinitionDescriptor Descriptor { get; }

    /// <summary>
    /// Dispatches the visitor to <see cref="IImportEntityVisitor{TResult}.Visit{TEntity}"/>
    /// with the statically-typed definition.
    /// </summary>
    /// <typeparam name="TResult">The visitor result type.</typeparam>
    /// <param name="visitor">The visitor to dispatch.</param>
    /// <returns>The visitor's result.</returns>
    TResult Accept<TResult>(IImportEntityVisitor<TResult> visitor);
}
