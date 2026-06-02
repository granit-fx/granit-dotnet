using System.Reflection;
using System.Runtime.CompilerServices;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable Wolverine handler convention rules.
/// </summary>
public static class WolverineHandlerConventionRules
{
    /// <summary>
    /// Assemblies that contain internal Wolverine handlers must declare
    /// <c>[InternalsVisibleTo("WolverineHandlers")]</c>.
    /// Without this, Wolverine silently skips the handler at runtime — no error, no message.
    /// </summary>
    /// <param name="callerAssembly">The test assembly — used to locate the output directory.</param>
    /// <param name="assemblyPrefix">Prefix for assemblies to scan (e.g. <c>"Granit."</c>).</param>
    public static void AssembliesWithInternalHandlersMustExposeInternalsToWolverineHandlers(
        Assembly callerAssembly,
        string assemblyPrefix)
    {
        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;

        Assembly[] assemblies = Directory.GetFiles(outputDir, $"{assemblyPrefix}*.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests", StringComparison.Ordinal))
            .Select(TryLoadAssembly)
            .Where(a => a is not null)
            .ToArray()!;

        List<string> violations = [];

        foreach (Assembly assembly in assemblies)
        {
            Type[] internalHandlers;
            try
            {
                internalHandlers = [.. assembly.GetTypes()
                    .Where(t => !t.IsPublic && !t.IsAbstract && !t.IsInterface)
                    .Where(HasWolverineHandlerMethod)];
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            if (internalHandlers.Length == 0)
            {
                continue;
            }

            bool exposesInternals = assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
                .Any(a => a.AssemblyName == "WolverineHandlers");

            if (!exposesInternals)
            {
                string handlerNames = string.Join(", ", internalHandlers.Select(t => t.Name));
                violations.Add(
                    $"{assembly.GetName().Name} has internal handler(s) [{handlerNames}] " +
                    "but is missing [InternalsVisibleTo(\"WolverineHandlers\")] in .csproj");
            }
        }

        violations.ShouldBeEmpty(
            "Assemblies with internal Wolverine handlers must declare " +
            "<InternalsVisibleTo Include=\"WolverineHandlers\" /> in their .csproj. " +
            "Without this, Wolverine silently skips the handler at runtime.");
    }

    private static bool HasWolverineHandlerMethod(Type type)
    {
        bool looksLikeHandler = type.Name.EndsWith("Handler", StringComparison.Ordinal)
            || (type.Namespace?.Contains(".Handlers", StringComparison.Ordinal) ?? false);

        if (!looksLikeHandler)
        {
            return false;
        }

        return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(m => m.Name is "HandleAsync" or "Handle" or "ConsumeAsync" or "Consume");
    }

    private static Assembly? TryLoadAssembly(string path)
    {
        try { return Assembly.LoadFrom(path); }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
    }
}
