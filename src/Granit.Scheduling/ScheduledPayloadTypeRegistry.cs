using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Granit.Reflection;

namespace Granit.Scheduling;

/// <summary>
/// Allowlist of known <see cref="IScheduledPayload"/> types discovered at startup.
/// </summary>
/// <remarks>
/// <para>
/// Prevents confused-deputy attacks: only types registered here can be deserialized
/// from the <c>PayloadType</c> column in the database. Without this allowlist, an
/// attacker with database write access could set <c>PayloadType</c> to an arbitrary
/// CLR type and have it dispatched via Wolverine with the system's own identity.
/// </para>
/// <para>
/// Types are discovered lazily on first resolution by scanning all loaded assemblies
/// for concrete <see cref="IScheduledPayload"/> implementations.
/// </para>
/// </remarks>
public sealed class ScheduledPayloadTypeRegistry
{
    private readonly FrozenDictionary<string, Type> _typesByName;

    /// <summary>
    /// Initializes the registry by scanning all loaded assemblies for
    /// concrete <see cref="IScheduledPayload"/> implementations.
    /// </summary>
    public ScheduledPayloadTypeRegistry()
    {
        _typesByName = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeTypeLoader.GetLoadableTypes)
            .Where(t => t.IsAssignableTo(typeof(IScheduledPayload))
                         && t is { IsAbstract: false, IsInterface: false })
            .ToFrozenDictionary(t => t.AssemblyQualifiedName!, t => t);
    }

    /// <summary>
    /// Resolves a payload type by its assembly-qualified name.
    /// Returns <c>false</c> if the type is not in the allowlist.
    /// </summary>
    public bool TryResolve(
        string assemblyQualifiedName,
        [NotNullWhen(true)] out Type? type) =>
        _typesByName.TryGetValue(assemblyQualifiedName, out type);

    /// <summary>
    /// Resolves a payload type by its assembly-qualified name.
    /// Throws if the type is not in the allowlist.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type is not a registered <see cref="IScheduledPayload"/> implementation.
    /// </exception>
    public Type Resolve(string assemblyQualifiedName) =>
        TryResolve(assemblyQualifiedName, out Type? type)
            ? type
            : throw new InvalidOperationException(
                $"Payload type '{assemblyQualifiedName}' is not a registered IScheduledPayload implementation. "
                + "Ensure the type implements IScheduledPayload and its assembly is loaded at startup.");
}
