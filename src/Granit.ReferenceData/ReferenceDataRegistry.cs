using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Granit.ReferenceData;

/// <summary>
/// Singleton registry of all dynamically registered reference data types.
/// Populated at DI registration time by <c>AddReferenceData&lt;TDbContext&gt;()</c>.
/// </summary>
/// <remarks>
/// The registry is read at endpoint mapping time by <c>MapAllReferenceDataEndpoints()</c>
/// and at model-building time to create EF Core SharedTypeEntity configurations.
/// </remarks>
public sealed class ReferenceDataRegistry
{
    private readonly ConcurrentDictionary<string, ReferenceDataTypeRegistration> _types = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all registered reference data types.
    /// </summary>
    public IReadOnlyCollection<ReferenceDataTypeRegistration> Types => [.. _types.Values];

    /// <summary>
    /// Registers a reference data type. Called at DI registration time.
    /// </summary>
    /// <param name="registration">The type registration metadata.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a type with the same name is already registered.
    /// </exception>
    internal void Register(ReferenceDataTypeRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!_types.TryAdd(registration.TypeName, registration))
        {
            throw new InvalidOperationException(
                $"A reference data type named '{registration.TypeName}' is already registered.");
        }
    }

    /// <summary>
    /// Gets a registration by type name.
    /// </summary>
    /// <param name="typeName">The logical type name.</param>
    /// <param name="registration">The registration if found.</param>
    /// <returns><see langword="true"/> if the type is registered.</returns>
    public bool TryGet(string typeName, [NotNullWhen(true)] out ReferenceDataTypeRegistration? registration) =>
        _types.TryGetValue(typeName, out registration);
}
