using System.Collections.Concurrent;

namespace Granit.Taxonomy.Registration;

/// <summary>
/// Singleton registry mapping a <c>TargetType</c> discriminator to the scope under
/// which tags applied to that aggregate are managed.
/// </summary>
/// <remarks>
/// Each consumer module registers its taggable aggregates via
/// <c>services.AddTaggableEntity&lt;TAggregate&gt;(scope: "...")</c>. The assignment
/// endpoints validate the incoming <c>targetType</c> against this registry.
/// </remarks>
public sealed class TaggableTypeRegistry
{
    private readonly ConcurrentDictionary<string, string> _typeToScope = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers <paramref name="targetType"/> under <paramref name="scope"/>. Throws
    /// when a different scope is already registered for the same target type, to keep
    /// the (TargetType → Scope) mapping unambiguous.
    /// </summary>
    public void Register(string targetType, string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        _typeToScope.AddOrUpdate(
            targetType,
            scope,
            (_, existing) =>
            {
                if (!string.Equals(existing, scope, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Target type '{targetType}' is already registered under scope '{existing}'; " +
                        $"cannot re-register under '{scope}'.");
                }
                return existing;
            });
    }

    /// <summary>Returns <c>true</c> when <paramref name="targetType"/> is a registered taggable aggregate.</summary>
    public bool IsRegistered(string targetType) => _typeToScope.ContainsKey(targetType);

    /// <summary>Returns the scope registered for <paramref name="targetType"/>, or <c>null</c> when unregistered.</summary>
    public string? GetScope(string targetType) =>
        _typeToScope.TryGetValue(targetType, out string? scope) ? scope : null;

    /// <summary>Snapshot of the currently registered (targetType, scope) pairs.</summary>
    public IReadOnlyDictionary<string, string> Snapshot => _typeToScope.ToArray()
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
}
