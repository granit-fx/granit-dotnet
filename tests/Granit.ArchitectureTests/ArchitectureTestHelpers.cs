namespace Granit.ArchitectureTests;

/// <summary>
/// Shared filesystem helpers for the source-scan convention tests introduced by the
/// wiring-conventions suite (#2995). Pre-existing test classes keep their private
/// copies; new source-scan tests should use these.
/// </summary>
internal static class ArchitectureTestHelpers
{
    /// <summary>Walks up from the test assembly to the directory containing <c>.git</c>.</summary>
    public static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ArchitectureTestHelpers).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>Enumerates non-generated C# sources under <paramref name="dir"/> (skips bin/obj).</summary>
    public static IEnumerable<string> EnumerateSourceFiles(string dir) =>
        Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(static f =>
                !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    /// <summary>Returns the <c>src/&lt;Project&gt;</c> directory that contains <paramref name="file"/>.</summary>
    public static string ProjectDirOf(string srcDir, string file)
    {
        string relative = Path.GetRelativePath(srcDir, file);
        string projectName = relative.Split(Path.DirectorySeparatorChar)[0];
        return Path.Join(srcDir, projectName);
    }
}
