using System.Reflection;
using System.Runtime.CompilerServices;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates Wolverine handler conventions:
/// <list type="bullet">
/// <item>Assemblies with internal Wolverine handlers must expose internals to <c>WolverineHandlers</c></item>
/// </list>
/// </summary>
public sealed class WolverineHandlerConventionTests
{
    /// <summary>
    /// Wolverine compiles generated code into a <c>WolverineHandlers</c> assembly at runtime.
    /// If a handler class is <c>internal</c>, the generated code cannot call it unless the
    /// source assembly declares <c>[InternalsVisibleTo("WolverineHandlers")]</c>.
    /// Without this, the handler is silently skipped — no error, no email, no notification.
    /// </summary>
    [Fact]
    public void Assemblies_with_internal_handlers_must_expose_internals_to_WolverineHandlers()
    {
        string outputDir = Path.GetDirectoryName(typeof(WolverineHandlerConventionTests).Assembly.Location)!;

        Assembly[] granitAssemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests"))
            .Select(TryLoadAssembly)
            .Where(a => a is not null)
            .ToArray()!;

        List<string> violations = [];

        foreach (Assembly assembly in granitAssemblies)
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
                    "but missing [InternalsVisibleTo(\"WolverineHandlers\")] in .csproj");
            }
        }

        violations.ShouldBeEmpty(
            "Assemblies with internal Wolverine handlers must declare " +
            "<InternalsVisibleTo Include=\"WolverineHandlers\" /> in their .csproj. " +
            "Without this, Wolverine silently skips the handler at runtime.");
    }

    /// <summary>
    /// Checks whether a type looks like a Wolverine handler: lives in a <c>Handlers</c> namespace
    /// or has a name ending in <c>Handler</c>, AND has at least one method matching Wolverine's
    /// naming conventions (<c>Handle</c>, <c>HandleAsync</c>, <c>Consume</c>, <c>ConsumeAsync</c>)
    /// whose first parameter is a message type (not a framework/DI service).
    /// </summary>
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
