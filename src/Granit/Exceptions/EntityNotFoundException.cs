namespace Granit.Exceptions;

/// <summary>
/// Exception thrown when a requested entity does not exist in the data store.
/// Maps to <c>404 Not Found</c>.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="IUserFriendlyException"/>: the client-facing message is a generic
/// "The requested resource was not found." to avoid leaking entity type names or identifiers.
/// Entity details are preserved in <see cref="EntityType"/> and <see cref="EntityId"/>
/// properties and in the <see cref="ToString"/> output (used by the logger).
/// </para>
/// <para>
/// Does NOT implement <see cref="IHasErrorCode"/> intentionally: entity type names and identifiers
/// must not be used as localizable keys, as they may inadvertently leak schema information.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// throw new EntityNotFoundException(typeof(Appointment), appointmentId);
/// </code>
/// </example>
public sealed class EntityNotFoundException : Exception, IUserFriendlyException
{
    /// <summary>The CLR type of the entity that was not found.</summary>
    public Type EntityType { get; }

    /// <summary>The identifier that was used in the lookup.</summary>
    public object EntityId { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="EntityNotFoundException"/>.
    /// </summary>
    /// <param name="entityType">CLR type of the missing entity.</param>
    /// <param name="id">Identifier used in the lookup.</param>
    public EntityNotFoundException(Type entityType, object id)
        : base("The requested resource was not found.")
    {
        EntityType = entityType;
        EntityId = id;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Includes entity type and identifier for diagnostic logging.
    /// This output is logged by <c>GranitExceptionHandler</c> but never exposed to clients.
    /// </remarks>
    public override string ToString() =>
        $"{GetType().FullName}: Entity '{EntityType.Name}' with id '{EntityId}' was not found." +
        (InnerException is not null ? $"\r\n ---> {InnerException}" : string.Empty) +
        $"\r\n{StackTrace}";
}
