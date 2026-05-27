namespace Granit.RateLimiting.Wolverine.Attributes;

/// <summary>
/// Marks a Wolverine message type as subject to rate limiting.
/// The <see cref="RateLimitMiddleware"/> reads this attribute to determine which
/// policy to enforce before the handler executes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public sealed class RateLimitedAttribute(string policyName) : Attribute
{
    /// <summary>Name of the rate limiting policy to apply.</summary>
    public string PolicyName { get; } = policyName;
}
