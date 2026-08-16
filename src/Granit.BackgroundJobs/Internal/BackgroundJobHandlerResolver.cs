using System.Collections.Concurrent;
using System.Reflection;
using Granit.Reflection;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Resolves the handler method for a background job message, mirroring Wolverine's
/// discovery rule so the in-process channel path and the Wolverine path bind the
/// same handler for the same message.
/// </summary>
/// <remarks>
/// <para>
/// Wolverine binds a handler method on the <b>type of its first parameter</b>. The class
/// name only gates discovery — it must end in <c>Handler</c> or <c>Consumer</c> — and the
/// method must be named <c>Handle</c>, <c>Handles</c>, <c>Consume</c>, <c>Consumes</c> or an
/// <c>Async</c> variant. So <c>MarkOverdueHandler.HandleAsync(MarkOverdueJob, …)</c> handles
/// <c>MarkOverdueJob</c>, which is the shape every <c>Granit.{Module}.BackgroundJobs</c>
/// satellite ships (CLAUDE.md documents the handler as <c>{Action}Handler</c>).
/// </para>
/// <para>
/// Candidates are ranked so the resolution is deterministic: an exactly-named
/// <c>{MessageTypeName}Handler</c> wins outright, then a handler whose first parameter is
/// exactly the message type, then one accepting a base type or interface. A tie at the best
/// rank is an ambiguity the caller must fix, so it throws rather than picking arbitrarily —
/// silently running one of two sweeps is worse than a loud failure.
/// </para>
/// </remarks>
internal static class BackgroundJobHandlerResolver
{
    private static readonly string[] HandlerMethodNames =
        ["HandleAsync", "Handle", "HandlesAsync", "Handles", "ConsumeAsync", "Consume", "ConsumesAsync", "Consumes"];

    private static readonly string[] HandlerTypeSuffixes = ["Handler", "Consumer"];

    private static readonly ConcurrentDictionary<Type, HandlerBinding> Cache = new();

    /// <summary>
    /// Returns the handler type and method bound to <paramref name="messageType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the message assembly exposes no matching handler, or more than one equally
    /// good candidate.
    /// </exception>
    internal static HandlerBinding Resolve(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return Cache.GetOrAdd(messageType, static type => ResolveCore(type));
    }

    private static HandlerBinding ResolveCore(Type messageType)
    {
        List<Candidate> candidates = [];

        foreach (Type type in messageType.Assembly.GetLoadableTypes())
        {
            if (!IsHandlerCandidate(type))
            {
                continue;
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (!HandlerMethodNames.Contains(method.Name, StringComparer.Ordinal)
                    || method.GetParameters() is not [{ } first, ..]
                    || !first.ParameterType.IsAssignableFrom(messageType))
                {
                    continue;
                }

                candidates.Add(new Candidate(type, method, Rank(type, first.ParameterType, messageType)));
            }
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No handler found in assembly '{messageType.Assembly.GetName().Name}' for message type " +
                $"'{messageType.Name}'. Expected a public non-generic type whose name ends in 'Handler' or " +
                "'Consumer', exposing a public HandleAsync/Handle (or Consume/Consumes) method taking " +
                $"'{messageType.Name}' as its first parameter.");
        }

        int bestRank = candidates.Min(c => c.Rank);
        List<Candidate> best = [.. candidates.Where(c => c.Rank == bestRank)
            .OrderBy(c => c.HandlerType.FullName, StringComparer.Ordinal)];

        // Several methods on the same handler type (overloads on the same message) are
        // still an ambiguity — only a single distinct binding is invocable here.
        if (best.Count > 1)
        {
            throw new InvalidOperationException(
                $"Ambiguous handler for message type '{messageType.Name}' in assembly " +
                $"'{messageType.Assembly.GetName().Name}': " +
                string.Join(", ", best.Select(c => $"{c.HandlerType.Name}.{c.Method.Name}")) +
                ". Keep exactly one handler method per job in the message's assembly.");
        }

        return new HandlerBinding(best[0].HandlerType, best[0].Method);
    }

    /// <summary>
    /// A concrete or static class whose name gates Wolverine discovery. Static classes are
    /// <c>abstract</c> + <c>sealed</c> at the CLR level, so only genuinely abstract types
    /// are excluded.
    /// </summary>
    private static bool IsHandlerCandidate(Type type) =>
        !type.IsInterface
        && !type.IsGenericTypeDefinition
        && (!type.IsAbstract || type.IsSealed)
        && HandlerTypeSuffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal));

    private static int Rank(Type handlerType, Type parameterType, Type messageType) =>
        handlerType.Name == messageType.Name + "Handler" ? 0
        : parameterType == messageType ? 1
        : 2;

    private readonly record struct Candidate(Type HandlerType, MethodInfo Method, int Rank);

    /// <summary>
    /// The handler type and the method to invoke for a given message type.
    /// </summary>
    internal readonly record struct HandlerBinding(Type HandlerType, MethodInfo Method);
}
