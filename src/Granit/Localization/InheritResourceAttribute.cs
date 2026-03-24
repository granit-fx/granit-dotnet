namespace Granit.Localization;

/// <summary>
/// Declares that this resource inherits translations from the specified parent resources.
/// Keys not found in this resource will be looked up in the parents.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class InheritResourceAttribute(params Type[] baseResourceTypes) : Attribute
{
    /// <summary>
    /// Types of the parent resources whose translations to inherit.
    /// </summary>
    public Type[] BaseResourceTypes { get; } = baseResourceTypes;
}
