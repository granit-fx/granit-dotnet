using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Architecture rules for the <c>Granit.Indexing.*</c> module family (epic #2234).
/// Pins the authorization-boundary contract, the VULN-201 atomic-delete invariant on
/// embeddings, and the layer-purity rules that close out the indexing horizontal
/// framework.
/// </summary>
public sealed partial class IndexingArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Join(RepoRoot, "src");

    /// <summary>
    /// Every <c>Granit.Indexing.*</c> package shipped at the time this test was authored.
    /// Add to this list when a new sub-module ships; the layer-purity tests below iterate
    /// it to ensure no sub-module quietly slips a forbidden dependency.
    /// </summary>
    private static readonly string[] IndexingPackages =
    [
        "Granit.Indexing",
        "Granit.Indexing.AI",
        "Granit.Indexing.BackgroundJobs",
        "Granit.Indexing.Elasticsearch",
        "Granit.Indexing.Embeddings",
        "Granit.Indexing.EntityFrameworkCore",
        "Granit.Indexing.Privacy",
    ];

    public static TheoryData<string> AllIndexingPackages
    {
        get
        {
            TheoryData<string> data = [];
            foreach (string p in IndexingPackages)
            {
                data.Add(p);
            }
            return data;
        }
    }

    /// <summary>
    /// VULN-003: <c>SearchPage&lt;TResult&gt;.BackendHitCount</c> is internal telemetry —
    /// it MUST NOT leak through any <c>.Endpoints</c> package, otherwise a malicious
    /// principal could use the value as a per-call existence oracle (see the property's
    /// remarks on <c>[JsonIgnore]</c> + <c>[EditorBrowsable(Never)]</c>).
    /// The framework currently ships no <c>Granit.Indexing.Endpoints</c> package; this test
    /// also protects against future consumers shipping endpoints that reach across the
    /// boundary.
    /// </summary>
    [Fact]
    public void BackendHitCount_must_not_be_referenced_from_any_Endpoints_package()
    {
        List<string> violators = [];

        foreach (string endpointsDir in Directory.EnumerateDirectories(SrcRoot, "*.Endpoints"))
        {
            foreach (string file in Directory.EnumerateFiles(endpointsDir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                if (text.Contains("BackendHitCount", StringComparison.Ordinal))
                {
                    violators.Add(Path.GetRelativePath(RepoRoot, file));
                }
            }
        }

        violators.ShouldBeEmpty(
            "SearchPage<TResult>.BackendHitCount is internal telemetry tagged "
            + "[JsonIgnore] + [EditorBrowsable(Never)] — it must never be projected, "
            + "logged, or wire-serialised by any .Endpoints package. Surfacing it gives "
            + "an attacker a per-query existence oracle. Violators: "
            + string.Join(", ", violators));
    }

    /// <summary>
    /// VULN-201: embeddings ARE personal data (CJEU 2024). The Granit framework persists
    /// them on the SAME row as <c>Content</c> so the existing
    /// <c>IIndexedDataEraser.EraseAsync</c> cascade purges both atomically. A sidecar
    /// table keyed only by the indexed entry's key would create a hard-to-cascade
    /// orphan and break Art. 17 conformance.
    /// </summary>
    [Fact]
    public void Embeddings_must_live_on_the_same_row_as_Content_no_sidecar_entity_types()
    {
        // We enforce by scanning for any class declaration whose name carries the
        // "Embedding" semantic AND looks like an EF Core entity (sealed class with a
        // public Key/Id-ish property OR has a Configuration<X> sibling). The base
        // IndexedEntryRow<TKey> is whitelisted because it carries Content alongside
        // Embedding — that IS the sanctioned shape.
        List<string> violators = [];

        foreach (string packageDir in EnumerateIndexingPackageDirs())
        {
            foreach (string file in Directory.EnumerateFiles(packageDir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(file))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                Match match = EmbeddingEntityClassDeclaration().Match(text);
                if (!match.Success)
                {
                    continue;
                }

                string className = match.Groups[1].Value;
                if (string.Equals(className, "IndexedEntryRow", StringComparison.Ordinal)
                    || string.Equals(className, "IndexedEntryDocument", StringComparison.Ordinal))
                {
                    // Sanctioned shapes — the row carries Content + Embedding together.
                    continue;
                }

                violators.Add($"{Path.GetRelativePath(RepoRoot, file)} -> {className}");
            }
        }

        violators.ShouldBeEmpty(
            "VULN-201: embedding columns must live on the same row as Content so the "
            + "GDPR Art. 17 cascade purges both atoms in a single statement. No sidecar "
            + "Embedding* entity types allowed in the Granit.Indexing.* family. "
            + "Sanctioned shapes: IndexedEntryRow (EF), IndexedEntryDocument (ES). "
            + "Violators: " + string.Join("; ", violators));
    }

    /// <summary>
    /// Defence-in-depth on the tenant query filter: <c>IgnoreQueryFilters</c> on
    /// <c>IndexedEntryRow&lt;TKey&gt;</c> queries is permitted ONLY in the three
    /// well-known call sites — the EF indexer's upsert lookup, the EF eraser's GDPR
    /// fan-out, and the EF vector backend's tenant-scoped kNN (which filters by tenant
    /// in the predicate itself). Any new <c>IgnoreQueryFilters</c> call site MUST be
    /// added to the allowlist below + reviewed for tenant safety.
    /// </summary>
    [Fact]
    public void IgnoreQueryFilters_calls_in_Granit_Indexing_EntityFrameworkCore_stay_within_the_audit_allowlist()
    {
        string[] allowlist =
        [
            "Internal/EfIndexer.cs",
            "Internal/EfIndexedDataEraser.cs",
            "Internal/EfRebuildCheckpointStore.cs",
        ];

        string packageDir = Path.Join(SrcRoot, "Granit.Indexing.EntityFrameworkCore");
        List<string> unexpected = [];

        foreach (string file in Directory.EnumerateFiles(packageDir, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            if (!text.Contains("IgnoreQueryFilters", StringComparison.Ordinal))
            {
                continue;
            }

            string rel = Path.GetRelativePath(packageDir, file).Replace('\\', '/');
            if (!allowlist.Contains(rel, StringComparer.Ordinal))
            {
                unexpected.Add(rel);
            }
        }

        unexpected.ShouldBeEmpty(
            "IgnoreQueryFilters opens a hole in the parameterised tenant filter shipped "
            + "by GranitDbContext. New call sites must be added to the architecture-test "
            + "allowlist after explicit tenant-safety review. Violators: "
            + string.Join(", ", unexpected));
    }

    /// <summary>
    /// Layer purity: the indexing framework ships no <c>.Endpoints</c> package, so no
    /// <c>Granit.Indexing.*</c> package should pull <c>Microsoft.AspNetCore.*</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllIndexingPackages))]
    public void Granit_Indexing_packages_must_not_reference_Microsoft_AspNetCore(string packageName) =>
        AssertNoPackageReferenceStartsWith(packageName, "Microsoft.AspNetCore.");

    /// <summary>
    /// Layer purity: EF Core dependencies are confined to the
    /// <c>Granit.Indexing.EntityFrameworkCore</c> backend.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllIndexingPackages))]
    public void EntityFrameworkCore_NuGets_only_in_the_EntityFrameworkCore_backend(string packageName)
    {
        if (string.Equals(packageName, "Granit.Indexing.EntityFrameworkCore", StringComparison.Ordinal))
        {
            return;
        }

        AssertNoPackageReferenceStartsWith(packageName, "Microsoft.EntityFrameworkCore");
        AssertNoPackageReferenceStartsWith(packageName, "Npgsql.EntityFrameworkCore");
        AssertNoPackageReferenceStartsWith(packageName, "Pgvector.EntityFrameworkCore");
    }

    /// <summary>
    /// Telemetry purity: <c>IIndexer&lt;TKey&gt;</c> implementations route observability
    /// through <c>IndexingMetrics</c> and <c>IndexingActivitySource</c>, never through
    /// raw <c>Console</c> / <c>Trace</c> calls — those break aggregation and leak
    /// content into stdout.
    /// </summary>
    [Fact]
    public void IIndexer_implementations_must_not_call_Console_or_Trace()
    {
        // Scan candidate files: any file matching *Indexer.cs (skip *IndexerTests.cs)
        // under src/Granit.Indexing*/. The framework's indexer impls follow this naming.
        List<string> violators = [];

        foreach (string packageDir in EnumerateIndexingPackageDirs())
        {
            foreach (string file in Directory.EnumerateFiles(packageDir, "*Indexer.cs", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(file))
                {
                    continue;
                }

                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith("IndexerTests.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                if (ConsoleOrTraceCall().IsMatch(text))
                {
                    violators.Add(Path.GetRelativePath(RepoRoot, file));
                }
            }
        }

        violators.ShouldBeEmpty(
            "IIndexer<TKey> implementations must emit observability through IndexingMetrics "
            + "and IndexingActivitySource — never via Console.* or Trace.* which break "
            + "structured aggregation and risk leaking indexed content. Violators: "
            + string.Join(", ", violators));
    }

    private static IEnumerable<string> EnumerateIndexingPackageDirs()
    {
        foreach (string name in IndexingPackages)
        {
            string dir = Path.Join(SrcRoot, name);
            if (Directory.Exists(dir))
            {
                yield return dir;
            }
        }
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static void AssertNoPackageReferenceStartsWith(string projectName, string forbiddenPrefix)
    {
        string csprojPath = Path.Join(SrcRoot, projectName, $"{projectName}.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        IEnumerable<string> packageRefs = doc.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty);

        IEnumerable<string> violators = packageRefs
            .Where(p => p.StartsWith(forbiddenPrefix, StringComparison.Ordinal));

        violators.ShouldBeEmpty(
            $"{projectName} must not reference NuGets starting with '{forbiddenPrefix}'. "
            + "Layer purity: indexing concerns stay below the HTTP / persistence boundaries. "
            + "Violators: " + string.Join(", ", violators));
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(IndexingArchitectureTests).Assembly.Location);
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
    /// Matches a class declaration whose simple name carries the "Embedding" semantic.
    /// Group 1 = the simple class name (e.g. <c>EmbeddingRow</c>, <c>EmbeddingDocument</c>,
    /// <c>IndexedEntryRow</c>). Generic type-parameter list (if any) is consumed but not
    /// captured.
    /// </summary>
    [GeneratedRegex(@"\bclass\s+(\w*(?:Embedding\w*Row|Embedding\w*Document|IndexedEntryRow|IndexedEntryDocument))\b")]
    private static partial Regex EmbeddingEntityClassDeclaration();

    /// <summary>
    /// Matches a call to <c>Console.&lt;anything&gt;</c> or <c>Trace.&lt;anything&gt;</c>
    /// — namespaces don't count (<c>System.Diagnostics.Trace</c> in xmldoc / using lines
    /// passes through because we require the identifier immediately followed by a dot
    /// + member access).
    /// </summary>
    [GeneratedRegex(@"\b(Console|Trace)\.[A-Z]\w*\s*\(", RegexOptions.Multiline)]
    private static partial Regex ConsoleOrTraceCall();
}
