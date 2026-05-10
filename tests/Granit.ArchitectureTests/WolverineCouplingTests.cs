using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Guard-rail that prevents business (domain) modules from silently reintroducing a
/// direct Wolverine coupling.
/// </summary>
/// <remarks>
/// <para>
/// Rationale: <c>ICommandSender</c> in <c>Granit</c> plus convention-based handler
/// discovery (public class + public static HandleAsync) make the Wolverine dependency
/// an implementation detail of <c>Granit.Wolverine</c>. Domain modules should not
/// <c>using Wolverine;</c> nor reference <c>WolverineFx</c> directly.
/// </para>
/// <para>
/// The allowlist covers packages whose name explicitly signals the coupling
/// (<c>Granit.Wolverine*</c>), the 3 adapters that keep the <c>.Wolverine</c> suffix
/// because they carry genuine Wolverine-specific infrastructure
/// (Scheduling, DataExchange, Persistence.EntityFrameworkCore.Migrations), and
/// <c>Granit.Events.Wolverine</c> / <c>Granit.BackgroundJobs.Wolverine</c>.
/// </para>
/// </remarks>
public sealed class WolverineCouplingTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");

    /// <summary>
    /// Projects whose direct Wolverine coupling is legitimate and part of their public
    /// contract. Names must match the csproj directory name.
    /// </summary>
    private static readonly HashSet<string> WolverineAdapterProjects = new(StringComparer.Ordinal)
    {
        // Core Wolverine infrastructure.
        "Granit.Wolverine",
        "Granit.Wolverine.Postgresql",
        "Granit.Wolverine.SqlServer",
        // Event bus and background jobs adapters (suffix == their whole purpose).
        "Granit.Events.Wolverine",
        "Granit.BackgroundJobs.Wolverine",
        // Adapters that stay .Wolverine because they carry Wolverine-specific infrastructure
        // (DeliveryOptions, middleware policies, retry policy, local queue routing).
        "Granit.Scheduling.Wolverine",
        "Granit.Webhooks.Wolverine",
        "Granit.Notifications.Wolverine",
        // Granit.Scheduling.BackgroundJobs carries the catch-up dispatcher which uses
        // Wolverine DeliveryOptions + custom headers tied to ScheduledActionStatusMiddleware.
        "Granit.Scheduling.BackgroundJobs",
        // Granit.Privacy defines Wolverine Sagas (scatter-gather export/deletion orchestration).
        // These use the Wolverine.Saga base type — genuine coupling at the core domain layer.
        "Granit.Privacy",
        // Granit.Wolverine.Encryption is a field-level encryption adapter that hooks into
        // the Wolverine envelope/saga JSON pipeline — the WolverineFx reference and Wolverine
        // namespace usage are intrinsic to the package's purpose.
        "Granit.Wolverine.Encryption",
    };

    [Fact]
    public void Domain_packages_should_not_use_Wolverine_namespace()
    {
        List<string> violations = [];

        foreach (string csFile in Directory.EnumerateFiles(SrcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcludedPath(csFile))
            {
                continue;
            }

            string projectName = GetProjectDirectoryName(csFile);
            if (WolverineAdapterProjects.Contains(projectName))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);
            if (HasWolverineUsing(content))
            {
                violations.Add(
                    $"{Path.GetRelativePath(RepoRoot, csFile)}: contains 'using Wolverine...'");
            }
        }

        violations.ShouldBeEmpty(
            "Domain (non-adapter) packages must not reference Wolverine types directly. " +
            "Inject Granit.Commands.ICommandSender from Granit and rely on handler " +
            "convention discovery. If a new adapter is legitimate, add it to " +
            $"{nameof(WolverineAdapterProjects)}.\nViolations:\n - " +
            string.Join("\n - ", violations));
    }

    [Fact]
    public void Domain_packages_should_not_reference_WolverineFx_package()
    {
        List<string> violations = [];

        foreach (string csproj in Directory.EnumerateFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories))
        {
            string projectName = Path.GetFileNameWithoutExtension(csproj);
            if (WolverineAdapterProjects.Contains(projectName))
            {
                continue;
            }

            string content = File.ReadAllText(csproj);
            if (content.Contains("Include=\"WolverineFx\"", StringComparison.Ordinal))
            {
                violations.Add($"{Path.GetRelativePath(RepoRoot, csproj)}: references WolverineFx");
            }
        }

        violations.ShouldBeEmpty(
            "Only adapter packages may reference the WolverineFx NuGet package. " +
            "Domain packages get Wolverine transitively via Granit.Wolverine when a host " +
            "opts into Wolverine.\nViolations:\n - " +
            string.Join("\n - ", violations));
    }

    private static bool HasWolverineUsing(string content)
    {
        foreach (string trimmed in content.Split('\n').Select(static line => line.TrimStart()))
        {
            if (trimmed.StartsWith("using Wolverine;", StringComparison.Ordinal) ||
                trimmed.StartsWith("using Wolverine.", StringComparison.Ordinal) ||
                trimmed.StartsWith("using static Wolverine", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsExcludedPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
               normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string GetProjectDirectoryName(string csFile)
    {
        string relative = Path.GetRelativePath(SrcRoot, csFile).Replace('\\', '/');
        int firstSep = relative.IndexOf('/');
        return firstSep > 0 ? relative[..firstSep] : relative;
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !Directory.Exists(Path.Combine(dir, "src")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        return dir;
    }
}
