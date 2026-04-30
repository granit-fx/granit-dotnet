namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Derives the module name shown in the discovery tree from an entity's wire
/// identifier. The wire id follows <c>Granit.{Module}.{Entity}</c> for framework
/// modules and <c>{App}.{Module}.{Entity}</c> for host applications — in either
/// case the module is the second-to-last dot segment.
/// </summary>
internal static class EntityModuleResolver
{
    /// <summary>
    /// Returns the module segment, or <c>"_"</c> when the wire id has fewer than
    /// two dot segments. The fallback is deliberately unprintable-ish so a
    /// missing prefix is obvious in admin UIs.
    /// </summary>
    public static string Resolve(string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        int lastDot = entityName.LastIndexOf('.');
        if (lastDot <= 0)
        {
            return "_";
        }

        ReadOnlySpan<char> head = entityName.AsSpan(0, lastDot);
        int prevDot = head.LastIndexOf('.');
        return prevDot < 0
            ? new string(head)
            : new string(head[(prevDot + 1)..]);
    }
}
