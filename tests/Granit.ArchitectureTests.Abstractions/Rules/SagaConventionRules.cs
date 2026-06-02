using System.Reflection;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable Wolverine saga convention rules.
/// Pass <c>typeof(Wolverine.Persistence.Sagas.Saga)</c> as the <c>sagaBaseType</c>
/// from the test project (which already depends on WolverineFx).
/// </summary>
public static class SagaConventionRules
{
    // Wolverine's SagaChain.findByNames matches these base names STRICTLY (no Async stripping).
    private static readonly string[] CodegenBaseNames =
    [
        "Start", "Starts",
        "Handle", "Handles",
        "Orchestrate", "Orchestrates",
        "Consume", "Consumes",
        "StartOrHandle", "StartsOrHandles",
        "NotFound",
    ];

    private static readonly string[] StartMethodNames =
    [
        "Start", "StartAsync", "Starts", "StartsAsync",
        "StartOrHandle", "StartOrHandleAsync", "StartsOrHandle", "StartsOrHandleAsync",
    ];

    /// <summary>
    /// Every message that starts a Wolverine saga must expose a resolvable saga identity member.
    /// Without it, Wolverine persists the saga with <c>Guid.Empty</c> and the PostgreSQL
    /// lightweight saga storage throws <c>ArgumentOutOfRangeException</c> at runtime.
    /// </summary>
    /// <param name="callerAssembly">Test assembly — used to locate DLLs.</param>
    /// <param name="assemblyPrefix">Prefix for assemblies to scan (e.g. <c>"Granit."</c>).</param>
    /// <param name="sagaBaseType">
    /// The Wolverine <c>Saga</c> base type. Pass <c>typeof(Wolverine.Persistence.Sagas.Saga)</c>.
    /// Provided as a parameter to avoid a hard dependency on WolverineFx in this package.
    /// </param>
    public static void SagaStartMessagesMustHaveResolvableIdentity(
        Assembly callerAssembly,
        string assemblyPrefix,
        Type sagaBaseType)
    {
        List<string> violations = [];

        foreach (Assembly? assembly in LoadAssemblies(callerAssembly, assemblyPrefix))
        {
            Type[] sagaTypes;
            try { sagaTypes = [.. assembly!.GetTypes().Where(t => !t.IsAbstract && sagaBaseType.IsAssignableFrom(t))]; }
            catch (ReflectionTypeLoadException) { continue; }

            foreach (Type sagaType in sagaTypes)
            {
                foreach (MethodInfo method in GetStartMethods(sagaType))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        continue;
                    }

                    ParameterInfo msgParam = parameters[0];
                    Type msgType = msgParam.ParameterType;

                    if (!HasResolvableSagaIdentity(sagaType, msgParam, msgType))
                    {
                        violations.Add(
                            $"{sagaType.FullName}.{method.Name}({msgType.Name}): add [SagaIdentity] on a property, " +
                            $"or rename it to '{sagaType.Name}Id' / 'SagaId' / 'Id'.");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every message that starts a Wolverine saga must expose a resolvable saga identity. " +
            "Without it, Wolverine persists with Guid.Empty and PostgreSQL lightweight storage throws at runtime.");
    }

    /// <summary>
    /// Saga methods that map to Wolverine codegen slots (Start/Handle/Orchestrate/Consume/NotFound)
    /// must NOT carry the <c>Async</c> suffix — <c>SagaChain.findByNames</c> matches them strictly
    /// and silently drops Async-suffixed variants, producing a saga that is never initialized.
    /// </summary>
    public static void SagaMethodsMustNotUseAsyncSuffixForCodegenNames(
        Assembly callerAssembly,
        string assemblyPrefix,
        Type sagaBaseType)
    {
        List<string> violations = [];

        foreach (Assembly? assembly in LoadAssemblies(callerAssembly, assemblyPrefix))
        {
            Type[] sagaTypes;
            try { sagaTypes = [.. assembly!.GetTypes().Where(t => !t.IsAbstract && sagaBaseType.IsAssignableFrom(t))]; }
            catch (ReflectionTypeLoadException) { continue; }

            foreach (Type sagaType in sagaTypes)
            {
                foreach (MethodInfo method in sagaType.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!method.Name.EndsWith("Async", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string baseName = method.Name[..^"Async".Length];
                    if (CodegenBaseNames.Contains(baseName, StringComparer.Ordinal))
                    {
                        violations.Add(
                            $"{sagaType.FullName}.{method.Name}: rename to '{baseName}'. " +
                            "SagaChain.findByNames does not strip 'Async' — this method is silently skipped.");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "Saga methods for codegen slots (Start/Handle/Orchestrate/Consume/NotFound) must NOT " +
            "carry the 'Async' suffix — SagaChain silently drops them, leaving Id = Guid.Empty at runtime.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Assembly?[] LoadAssemblies(Assembly callerAssembly, string assemblyPrefix)
    {
        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;
        return Directory.GetFiles(outputDir, $"{assemblyPrefix}*.dll")
            .Where(p => !Path.GetFileNameWithoutExtension(p).Contains("Tests", StringComparison.Ordinal))
            .Select(TryLoad)
            .Where(a => a is not null)
            .ToArray();
    }

    private static Assembly? TryLoad(string path)
    {
        try { return Assembly.LoadFrom(path); }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
    }

    private static IEnumerable<MethodInfo> GetStartMethods(Type sagaType) =>
        sagaType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => StartMethodNames.Contains(m.Name, StringComparer.Ordinal));

    private static bool HasResolvableSagaIdentity(Type sagaType, ParameterInfo parameter, Type messageType)
    {
        // [SagaIdentityFrom] on the parameter
        foreach (Attribute attr in parameter.GetCustomAttributes())
        {
            if (attr.GetType().Name == "SagaIdentityFromAttribute")
            {
                object? propName = attr.GetType().GetProperty("PropertyName")?.GetValue(attr);
                if (propName is string name && HasMember(messageType, name))
                {
                    return true;
                }
            }
        }

        // [SagaIdentity] on a message member
        if (GetMessageMembers(messageType).Any(m => m.GetCustomAttributes()
                .Any(a => a.GetType().Name == "SagaIdentityAttribute")))
        {
            return true;
        }

        // Conventional property names
        string sagaName = sagaType.Name;
        string sagaWithoutSuffix = sagaName.EndsWith("Saga", StringComparison.Ordinal) ? sagaName[..^4] : sagaName;
        return new[] { $"{sagaName}Id", $"{sagaWithoutSuffix}Id", "SagaId", "Id" }
            .Any(name => HasMember(messageType, name));
    }

    private static bool HasMember(Type type, string memberName) =>
        type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance) is not null
        || type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance) is not null;

    private static IEnumerable<MemberInfo> GetMessageMembers(Type messageType)
    {
        foreach (PropertyInfo p in messageType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return p;
        }

        foreach (FieldInfo f in messageType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return f;
        }
    }
}
