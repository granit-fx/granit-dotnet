namespace Granit.Bulkhead.Wolverine.Attributes;

/// <summary>
/// Marks a Wolverine message type as subject to bulkhead isolation.
/// The <see cref="Wolverine.BulkheadMiddleware"/> reads this attribute to determine which
/// policy to enforce before the handler executes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public sealed class BulkheadAttribute(string policyName) : Attribute
{
    /// <summary>Name of the bulkhead policy to apply.</summary>
    public string PolicyName { get; } = policyName;
}
