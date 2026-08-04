using System.Reflection;
using System.Text.RegularExpressions;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Cross-checks every module's <c>[DependsOn]</c> declaration against its project's direct
/// <c>&lt;ProjectReference&gt;</c> graph (CLAUDE.md §[DependsOn] rules): a direct project
/// reference that exposes a <see cref="GranitModule"/> MUST be declared; transitive-only
/// modules MUST NOT be; <c>Granit</c> is the implicit base and is never declared; the
/// argument list is alphabetical. Before this guard, omissions were invisible to CI — the
/// 2026-08-04 persistence audit found three in the Persistence family alone (#3153).
/// </summary>
public sealed partial class DependsOnProjectReferenceConventionTests
{
    [GeneratedRegex("""<ProjectReference\s+Include="(?<path>[^"]+)"\s*/?>""")]
    private static partial Regex ProjectReferenceRegex();

    /// <summary>
    /// RATCHET (#3172) — projects whose <c>[DependsOn]</c> predates this guard and still omits
    /// modules exposed by direct project references (overwhelmingly <c>*AbstractionsModule</c>
    /// deps). The missing-declaration assertion is skipped for these; the no-transitive and
    /// alphabetical assertions still apply. DO NOT add entries — fix the module's
    /// <c>[DependsOn]</c> instead. Remove an entry once its module declares its direct refs.
    /// </summary>
    private static readonly HashSet<string> KnownMissingDeclarations = new(StringComparer.Ordinal)
    {
        // Baseline frozen 2026-08-04 from the first repo-wide run (74 projects) — see #3172.
        "Granit.AI",
        "Granit.AI.Chat",
        "Granit.AI.Chat.Endpoints",
        "Granit.AI.Endpoints",
        "Granit.AI.Prompts",
        "Granit.AI.Prompts.Endpoints",
        "Granit.AI.Prompts.EntityFrameworkCore",
        "Granit.Authentication.ApiKeys",
        "Granit.Authentication.ApiKeys.Endpoints",
        "Granit.Authentication.Mtls",
        "Granit.Authorization",
        "Granit.Authorization.Endpoints",
        "Granit.Authorization.EntityFrameworkCore",
        "Granit.BackgroundJobs",
        "Granit.BackgroundJobs.EntityFrameworkCore",
        "Granit.Bff.Endpoints",
        "Granit.Bff.EntityFrameworkCore",
        "Granit.Bff.Yarp",
        "Granit.BlobStorage",
        "Granit.BlobStorage.GoogleCloud",
        "Granit.BlobStorage.Proxy",
        "Granit.BlobStorage.S3",
        "Granit.Browsing",
        "Granit.Caching.StackExchangeRedis",
        "Granit.DataLookup.EntityFrameworkCore",
        "Granit.Entities.Abstractions",
        "Granit.EntityMerge.EntityFrameworkCore",
        "Granit.Features.EntityFrameworkCore",
        "Granit.Hostnames",
        "Granit.Hostnames.Endpoints",
        "Granit.Http.Cookies.Endpoints",
        "Granit.Http.Idempotency.StackExchangeRedis",
        "Granit.Http.ODataExposure",
        "Granit.Http.OutputCaching.StackExchangeRedis",
        "Granit.Identity",
        "Granit.Identity.Abstractions",
        "Granit.Identity.Endpoints",
        "Granit.Identity.EntityFrameworkCore",
        "Granit.Identity.Federated",
        "Granit.Identity.Federated.Cognito",
        "Granit.Identity.Federated.EntraId",
        "Granit.Identity.Federated.Keycloak",
        "Granit.Identity.Local",
        "Granit.Identity.Local.AspNetIdentity",
        "Granit.Identity.Local.Endpoints",
        "Granit.Indexing.Elasticsearch",
        "Granit.Indexing.EntityFrameworkCore",
        "Granit.Localization",
        "Granit.Localization.Endpoints",
        "Granit.Localization.EntityFrameworkCore",
        "Granit.Mcp.Server",
        "Granit.Mentions",
        "Granit.MultiTenancy.Auditing",
        "Granit.MultiTenancy.Authorization",
        "Granit.Notifications.Abstractions",
        "Granit.Notifications.Sse",
        "Granit.Notifications.WhatsApp",
        "Granit.Presence.Endpoints",
        "Granit.Privacy",
        "Granit.Privacy.Endpoints",
        "Granit.Privacy.EntityFrameworkCore",
        "Granit.QueryEngine.AI",
        "Granit.QueryEngine.Abstractions",
        "Granit.QueryEngine.Endpoints",
        "Granit.Scheduling",
        "Granit.Scheduling.EntityFrameworkCore",
        "Granit.Settings",
        "Granit.Settings.EntityFrameworkCore",
        "Granit.Templating",
        "Granit.Templating.EntityFrameworkCore",
        "Granit.Testing",
        "Granit.Webhooks",
        "Granit.Webhooks.Endpoints",
        "Granit.Workflow",
    };

    public static TheoryData<string> ModuleProjects()
    {
        TheoryData<string> data = [];
        foreach (string csproj in EnumerateSrcProjects())
        {
            if (LoadModules(Path.GetFileNameWithoutExtension(csproj)).Count > 0)
            {
                data.Add(Path.GetFileNameWithoutExtension(csproj));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void DependsOn_matches_direct_project_references(string projectName)
    {
        string csproj = EnumerateSrcProjects()
            .Single(p => Path.GetFileNameWithoutExtension(p) == projectName);

        // Expected: every DIRECT ProjectReference whose assembly exposes a GranitModule,
        // except the implicit base assembly "Granit".
        List<Type> expected = [];
        foreach (string referencedProject in DirectProjectReferences(csproj))
        {
            if (referencedProject == "Granit")
            {
                continue;
            }

            expected.AddRange(LoadModules(referencedProject));
        }

        // Actual: union of [DependsOn] across the project's module classes (almost always one).
        IReadOnlyList<Type> modules = LoadModules(projectName);
        List<Type> declared = [.. modules
            .SelectMany(m => m.GetCustomAttributes<DependsOnAttribute>(inherit: false))
            .SelectMany(a => a.DependedTypes)];

        string moduleNames = string.Join(", ", modules.Select(m => m.Name));

        List<string> missing = [.. expected
            .Where(e => !declared.Contains(e))
            .Select(e => e.Name)
            .Order(StringComparer.Ordinal)];
        List<string> extra = [.. declared
            .Where(d => !expected.Contains(d))
            .Select(d => d.Name)
            .Order(StringComparer.Ordinal)];

        if (KnownMissingDeclarations.Contains(projectName))
        {
            // Ratchet (#3172): tolerate pre-existing omissions, but flag a resolved entry so
            // the baseline shrinks as modules are fixed.
            missing.ShouldNotBeEmpty(
                $"{projectName} no longer has missing [DependsOn] declarations — remove it from "
                + "KnownMissingDeclarations (ratchet #3172).");
            missing = [];
        }

        missing.ShouldBeEmpty(
            $"{moduleNames}: direct ProjectReferences expose these modules but [DependsOn] omits them "
            + $"(declare direct refs, CLAUDE.md §[DependsOn]): {string.Join(", ", missing)}");
        extra.ShouldBeEmpty(
            $"{moduleNames}: [DependsOn] declares modules with no matching direct ProjectReference "
            + $"(transitive deps must be omitted): {string.Join(", ", extra)}");

        // Alphabetical order, per attribute.
        foreach (Type module in modules)
        {
            foreach (DependsOnAttribute attribute in module.GetCustomAttributes<DependsOnAttribute>(inherit: false))
            {
                string[] names = [.. attribute.DependedTypes.Select(x => x.Name)];
                names.ShouldBe([.. names.Order(StringComparer.Ordinal)],
                    $"{module.Name}: [DependsOn] arguments must be alphabetical.");
            }
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static IEnumerable<string> EnumerateSrcProjects()
    {
        string srcDir = Path.Combine(ArchitectureTestHelpers.FindRepoRoot(), "src");
        return Directory.EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories);
    }

    private static IEnumerable<string> DirectProjectReferences(string csproj)
    {
        string content = File.ReadAllText(csproj);
        foreach (Match match in ProjectReferenceRegex().Matches(content))
        {
            yield return Path.GetFileNameWithoutExtension(
                match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar));
        }
    }

    /// <summary>
    /// Loads the public, non-abstract <see cref="GranitModule"/> subclasses of a src assembly.
    /// Assemblies absent from the test output (netstandard analyzers, bundles, exe tools — see
    /// <see cref="ArchitectureTestCoverageTests"/> exemptions) yield an empty list and are skipped.
    /// </summary>
    private static IReadOnlyList<Type> LoadModules(string assemblyName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
        if (!File.Exists(path))
        {
            return [];
        }

        var assembly = Assembly.Load(new AssemblyName(assemblyName));
        return [.. assembly.GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(GranitModule).IsAssignableFrom(t))];
    }
}
