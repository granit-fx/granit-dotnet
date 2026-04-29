namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Captures one EntitySet registration: the route segment, the CLR entity
/// type, the <c>QueryDefinition&lt;TEntity&gt;</c> CLR type, and the optional
/// permission gate. Built up by <see cref="Options.ODataExposureOptions"/>
/// at host configuration time, consumed by the route helper to wire one
/// minimal-API endpoint per set + the shared EDM model.
/// </summary>
/// <param name="EntitySetName">Route segment AND OData EntitySet name (e.g. <c>"Invoices"</c>).</param>
/// <param name="EntityType">CLR type of the entity.</param>
/// <param name="QueryDefinitionType">CLR type of the <c>QueryDefinition&lt;TEntity&gt;</c> backing this set — resolved through DI at request time.</param>
/// <param name="RequiredPermission">Permission name a request must carry to read this EntitySet, or <see langword="null"/> if any authenticated user is allowed (auth itself is enforced by the surrounding pipeline).</param>
internal sealed record ODataEntitySetDescriptor(
    string EntitySetName,
    Type EntityType,
    Type QueryDefinitionType,
    string? RequiredPermission);
