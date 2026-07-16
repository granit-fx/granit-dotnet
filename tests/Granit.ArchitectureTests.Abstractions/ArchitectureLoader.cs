using System.Reflection;
using ArchUnitNET.Loader;

namespace Granit.ArchitectureTests.Abstractions;

/// <summary>
/// Loads assemblies into an ArchUnitNET architecture graph by scanning output directories.
/// </summary>
public static class ArchitectureLoader
{
    /// <summary>
    /// Loads all assemblies matching the given prefix from the output directory of the calling assembly.
    /// Excludes test, analyzer, and source generator assemblies.
    /// </summary>
    /// <param name="assemblyPrefix">Assembly name prefix (e.g. "Granit.", "MyApp.").</param>
    /// <param name="callerAssembly">The test assembly whose output directory to scan.</param>
    /// <param name="additionalExclusions">Extra name fragments to exclude (e.g. "CodeFixes").</param>
    public static ArchUnitNET.Domain.Architecture Load(
        string assemblyPrefix,
        Assembly callerAssembly,
        params string[] additionalExclusions)
    {
        ArgumentNullException.ThrowIfNull(assemblyPrefix);
        ArgumentNullException.ThrowIfNull(callerAssembly);

        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;

        Assembly[] assemblies = Directory.GetFiles(outputDir, $"{assemblyPrefix}*.dll")
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.Contains("Analyzers", StringComparison.Ordinal)
                    && !name.Contains("SourceGenerator", StringComparison.Ordinal)
                    && !additionalExclusions.Any(ex =>
                        name.Contains(ex, StringComparison.Ordinal));
            })
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch { return null; }
            })
            .Where(a => a is not null)
            .ToArray()!;

        return new ArchLoader()
            .LoadAssemblies(assemblies)
            .Build();
    }

    /// <summary>
    /// Returns the framework assemblies (matching <paramref name="assemblyPrefix"/>, excluding test /
    /// analyzer / source-generator dlls) from the caller's output directory. Use for reflection-based
    /// conventions that need real <see cref="System.Type"/> metadata beyond what ArchUnitNET surfaces.
    /// </summary>
    public static IReadOnlyList<Assembly> LoadAssemblies(
        string assemblyPrefix,
        Assembly callerAssembly,
        params string[] additionalExclusions)
    {
        ArgumentNullException.ThrowIfNull(assemblyPrefix);
        ArgumentNullException.ThrowIfNull(callerAssembly);

        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;

        return Directory.GetFiles(outputDir, $"{assemblyPrefix}*.dll")
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.Contains("Analyzers", StringComparison.Ordinal)
                    && !name.Contains("SourceGenerator", StringComparison.Ordinal)
                    && !additionalExclusions.Any(ex => name.Contains(ex, StringComparison.Ordinal));
            })
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch { return null; }
            })
            .Where(a => a is not null)
            .ToList()!;
    }

    /// <summary>
    /// Loads assemblies matching multiple prefixes into a single architecture graph.
    /// </summary>
    public static ArchUnitNET.Domain.Architecture Load(
        string[] assemblyPrefixes,
        Assembly callerAssembly,
        params string[] additionalExclusions)
    {
        ArgumentNullException.ThrowIfNull(assemblyPrefixes);
        ArgumentNullException.ThrowIfNull(callerAssembly);

        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;

        Assembly[] assemblies = assemblyPrefixes
            .SelectMany(prefix => Directory.GetFiles(outputDir, $"{prefix}*.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.Contains("Analyzers", StringComparison.Ordinal)
                    && !name.Contains("SourceGenerator", StringComparison.Ordinal)
                    && !additionalExclusions.Any(ex =>
                        name.Contains(ex, StringComparison.Ordinal));
            })
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch { return null; }
            })
            .Where(a => a is not null)
            .ToArray()!;

        return new ArchLoader()
            .LoadAssemblies(assemblies)
            .Build();
    }
}
