using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates project-level dependency rules by scanning .csproj files:
/// - No circular project references (Project A → B → A)
/// </summary>
public sealed partial class ProjectDependencyTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Granit_MultiTenancy_must_not_reference_Granit_Authorization()
    {
        // Multi-tenancy is infrastructure (routing, claims, headers). Authorization is
        // application domain (permissions, RBAC). Infra must not depend on domain.
        // The permission-based host-impersonation gate lives in the glue package
        // Granit.MultiTenancy.Authorization, which references both.
        string csproj = Path.Join(RepoRoot, "src", "Granit.MultiTenancy", "Granit.MultiTenancy.csproj");
        File.Exists(csproj).ShouldBeTrue($"Expected csproj at {csproj}");

        string content = File.ReadAllText(csproj);
        IEnumerable<string> refs = ProjectReferenceInclude().Matches(content)
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value));

        refs.ShouldNotContain(
            "Granit.Authorization",
            "Granit.MultiTenancy.csproj must not reference Granit.Authorization. " +
            "Permission-based gating belongs in Granit.MultiTenancy.Authorization (glue package). " +
            "See IHostImpersonationGate.");
    }

    [Fact]
    public void No_circular_project_references()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        // Build adjacency list: project name → set of referenced project names
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);

        foreach (string csproj in Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories))
        {
            string projectName = Path.GetFileNameWithoutExtension(csproj);
            string content = File.ReadAllText(csproj);

            var references = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in ProjectReferenceInclude().Matches(content))
            {
                string refPath = match.Groups[1].Value;
                string refName = Path.GetFileNameWithoutExtension(refPath);
                references.Add(refName);
            }

            graph[projectName] = references;
        }

        // Detect cycles using DFS with coloring (white=unvisited, gray=in-stack, black=done)
        HashSet<string> visited = new(StringComparer.Ordinal);
        HashSet<string> inStack = new(StringComparer.Ordinal);
        List<string> cycles = [];

        foreach (string project in graph.Keys)
        {
            if (!visited.Contains(project))
            {
                DetectCycles(project, graph, visited, inStack, [], cycles);
            }
        }

        cycles.ShouldBeEmpty(
            "Circular project references detected — this causes build failures or runtime DI loops. " +
            $"Cycles: {string.Join("; ", cycles)}");
    }

    private static void DetectCycles(
        string node,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<string> path,
        List<string> cycles)
    {
        visited.Add(node);
        inStack.Add(node);
        path.Add(node);

        if (graph.TryGetValue(node, out HashSet<string>? neighbors))
        {
            foreach (string neighbor in neighbors)
            {
                if (inStack.Contains(neighbor))
                {
                    // Found a cycle — extract the cycle path
                    int cycleStart = path.IndexOf(neighbor);
                    IEnumerable<string> cyclePath = path.Skip(cycleStart).Append(neighbor);
                    cycles.Add(string.Join(" → ", cyclePath));
                }
                else if (!visited.Contains(neighbor))
                {
                    DetectCycles(neighbor, graph, visited, inStack, path, cycles);
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        inStack.Remove(node);
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ProjectDependencyTests).Assembly.Location);
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

    /// <summary>
    /// Extracts the Include path from ProjectReference elements.
    /// </summary>
    [GeneratedRegex(@"<ProjectReference\s+Include=""([^""]+)""")]
    private static partial Regex ProjectReferenceInclude();
}
