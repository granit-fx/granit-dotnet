using System.Reflection;
using Shouldly;
using Wolverine;
using Wolverine.Persistence.Sagas;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates Wolverine saga conventions.
/// </summary>
public sealed class SagaConventionTests
{
    private static readonly string[] StartMethodNames =
    [
        "Start",
        "StartAsync",
        "Starts",
        "StartsAsync",
        "StartOrHandle",
        "StartOrHandleAsync",
        "StartsOrHandle",
        "StartsOrHandleAsync",
    ];

    // Base names Wolverine's SagaChain.findByNames matches STRICTLY (no `Async` stripping,
    // unlike general HandlerDiscovery). Suffixing any of these with `Async` on a Saga
    // produces a method that's discovered as a handler but silently filtered out of the
    // generated chain — leading to a saga that's constructed but never initialized
    // (Id stays Guid.Empty) and then fails lightweight-storage insert at runtime with
    // `ArgumentOutOfRangeException: You must define the saga id`.
    //
    // Upstream reference: SagaChain.DetermineFrames in WolverineFx.
    private static readonly string[] CodegenBaseNames =
    [
        "Start", "Starts",
        "Handle", "Handles",
        "Orchestrate", "Orchestrates",
        "Consume", "Consumes",
        "StartOrHandle", "StartsOrHandles",
        "NotFound",
    ];

    /// <summary>
    /// Wolverine resolves the saga id from the initiating message via (in order):
    /// <c>[SagaIdentity]</c>, <c>[SagaIdentityFrom]</c> on the parameter,
    /// <c>{SagaType}Id</c>, <c>{SagaTypeWithoutSagaSuffix}Id</c>, <c>SagaId</c>, <c>Id</c>.
    /// If none match, the saga is persisted with <see cref="Guid.Empty"/> and the
    /// lightweight storage throws <c>ArgumentOutOfRangeException</c> at insert time.
    /// This test ensures every Start message carries a resolvable saga id member.
    /// </summary>
    [Fact]
    public void Saga_start_messages_must_expose_a_resolvable_saga_identity()
    {
        string outputDir = Path.GetDirectoryName(typeof(SagaConventionTests).Assembly.Location)!;

        Assembly[] granitAssemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests"))
            .Select(TryLoadAssembly)
            .Where(a => a is not null)
            .ToArray()!;

        List<string> violations = [];

        foreach (Assembly assembly in granitAssemblies)
        {
            Type[] sagaTypes;
            try
            {
                sagaTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && typeof(Saga).IsAssignableFrom(t))
                    .ToArray();
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (Type sagaType in sagaTypes)
            {
                foreach (MethodInfo method in GetStartMethods(sagaType))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        continue;
                    }

                    ParameterInfo messageParameter = parameters[0];
                    Type messageType = messageParameter.ParameterType;

                    if (HasResolvableSagaIdentity(sagaType, messageParameter, messageType))
                    {
                        continue;
                    }

                    violations.Add(
                        $"{sagaType.FullName}.{method.Name}({messageType.Name}): message '{messageType.Name}' has no member " +
                        $"resolvable as the saga identity. Add [SagaIdentity] on a property, or rename it to " +
                        $"'{sagaType.Name}Id' / 'SagaId' / 'Id'.");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every message that starts a Wolverine saga must expose a resolvable saga identity. " +
            "Without it, Wolverine persists the saga with Guid.Empty and the PostgreSQL " +
            "lightweight saga storage throws ArgumentOutOfRangeException at runtime.");
    }

    /// <summary>
    /// Wolverine's <c>SagaChain.findByNames</c> matches codegen-relevant method names
    /// strictly — it does NOT strip the <c>Async</c> suffix the way general
    /// <c>HandlerDiscovery</c> does. Declaring <c>StartAsync</c> / <c>HandleAsync</c>
    /// (or <c>OrchestrateAsync</c>, <c>ConsumeAsync</c>, etc.) on a Saga therefore
    /// produces a silently-skipped handler: the saga is constructed but its method is
    /// never invoked, so <c>Id</c> stays <see cref="Guid.Empty"/> and lightweight
    /// saga storage throws at insert time.
    /// </summary>
    [Fact]
    public void Saga_methods_must_not_use_Async_suffix_for_codegen_names()
    {
        string outputDir = Path.GetDirectoryName(typeof(SagaConventionTests).Assembly.Location)!;

        Assembly[] granitAssemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests"))
            .Select(TryLoadAssembly)
            .Where(a => a is not null)
            .ToArray()!;

        List<string> violations = [];

        foreach (Assembly assembly in granitAssemblies)
        {
            Type[] sagaTypes;
            try
            {
                sagaTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && typeof(Saga).IsAssignableFrom(t))
                    .ToArray();
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (Type sagaType in sagaTypes)
            {
                MethodInfo[] methods = sagaType.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (MethodInfo method in methods)
                {
                    if (!method.Name.EndsWith("Async", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string baseName = method.Name[..^"Async".Length];
                    if (!CodegenBaseNames.Contains(baseName, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    violations.Add(
                        $"{sagaType.FullName}.{method.Name}: rename to '{baseName}'. " +
                        "Wolverine's SagaChain.findByNames does not strip the 'Async' suffix, " +
                        "so this method is silently skipped by codegen even though it's " +
                        "visible to handler discovery.");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Saga methods that map to Wolverine codegen slots (Start/Handle/Orchestrate/" +
            "Consume/NotFound) must NOT carry the 'Async' suffix — SagaChain matches them " +
            "strictly and silently drops Async-suffixed variants, producing a saga that " +
            "is persisted with Guid.Empty and then fails lightweight-storage insert at " +
            "runtime.");
    }

    private static IEnumerable<MethodInfo> GetStartMethods(Type sagaType) =>
        sagaType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => StartMethodNames.Contains(m.Name, StringComparer.Ordinal));

    private static bool HasResolvableSagaIdentity(Type sagaType, ParameterInfo parameter, Type messageType)
    {
        if (parameter.GetCustomAttribute<SagaIdentityFromAttribute>() is { } fromAttr
            && HasMember(messageType, fromAttr.PropertyName))
        {
            return true;
        }

        if (GetMessageMembers(messageType).Any(m => m.GetCustomAttribute<SagaIdentityAttribute>() is not null))
        {
            return true;
        }

        string sagaName = sagaType.Name;
        string sagaNameWithoutSuffix = sagaName.EndsWith("Saga", StringComparison.Ordinal)
            ? sagaName[..^"Saga".Length]
            : sagaName;

        string[] conventionalNames =
        [
            $"{sagaName}Id",
            $"{sagaNameWithoutSuffix}Id",
            "SagaId",
            "Id",
        ];

        return conventionalNames.Any(name => HasMember(messageType, name));
    }

    private static bool HasMember(Type type, string memberName) =>
        type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance) is not null
        || type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance) is not null;

    private static IEnumerable<MemberInfo> GetMessageMembers(Type messageType)
    {
        foreach (PropertyInfo property in messageType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return property;
        }

        foreach (FieldInfo field in messageType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return field;
        }
    }

    private static Assembly? TryLoadAssembly(string path)
    {
        try
        {
            return Assembly.LoadFrom(path);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
        {
            return null;
        }
    }
}
