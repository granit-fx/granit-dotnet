namespace Granit.DataExchange.Export;

/// <summary>
/// Non-generic DI marker that carries an <see cref="ExportDefinition{TEntity}"/> together with
/// its compile-time entity type, recoverable through a generic visitor.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton alongside the definition by
/// <c>ExportDefinitionServiceCollectionExtensions.AddExportDefinition{TEntity,TDefinition}</c>.
/// The export pipeline registry enumerates <c>IEnumerable&lt;IExportEntityBinding&gt;</c> and uses
/// <see cref="Accept{TResult}"/> to build a typed pipeline per definition — replacing the
/// reflection bridges the orchestrator previously needed to recover <c>TEntity</c> at runtime.
/// </para>
/// </remarks>
public interface IExportEntityBinding
{
    /// <summary>
    /// The bound definition, viewed through its non-generic descriptor surface.
    /// </summary>
    IExportDefinitionDescriptor Descriptor { get; }

    /// <summary>
    /// Dispatches the strongly typed definition to the visitor.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by the visitor.</typeparam>
    /// <param name="visitor">The visitor receiving the typed definition.</param>
    /// <returns>The visitor result.</returns>
    TResult Accept<TResult>(IExportEntityVisitor<TResult> visitor);
}
