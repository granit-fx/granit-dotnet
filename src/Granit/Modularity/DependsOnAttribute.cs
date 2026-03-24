namespace Granit.Modularity;

/// <summary>
/// Declares a dependency on one or more Granit modules.
/// The system guarantees that dependent modules are loaded first.
/// Multiple <see cref="DependsOnAttribute"/> attributes may be stacked.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class DependsOnAttribute(params Type[] dependedTypes) : Attribute
{
    /// <summary>Types of the modules this module depends on.</summary>
    public Type[] DependedTypes { get; } = dependedTypes;
}
