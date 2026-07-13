
namespace Granit.Http.Idempotency;

/// <summary>
/// Marks an ASP.NET Core controller or action as idempotent.
/// </summary>
/// <remarks>
/// The middleware reads this attribute from endpoint metadata via <see cref="IIdempotencyMetadata"/>.
/// Compatible with Minimal APIs (via <c>.WithMetadata(new IdempotentAttribute())</c>),
/// MVC controllers, and Razor Pages.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentAttribute : Attribute, IIdempotencyMetadata
{
    /// <inheritdoc/>
    public bool Required { get; init; } = true;

    /// <inheritdoc/>
    public int CompletedTtlSeconds { get; init; } = -1;
}
