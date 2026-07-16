using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates the Isolated DbContext pattern checklist by scanning source files:
/// - OnModelCreating must call ApplyGranitConventions
/// - No manual HasQueryFilter (handled centrally by ApplyGranitConventions)
/// - *.EntityFrameworkCore.csproj must reference Granit.Persistence
/// </summary>
public sealed partial class IsolatedDbContextTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void OnModelCreating_should_call_ApplyGranitConventions()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            foreach (string csFile in Directory.GetFiles(efProject, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(csFile);

                // Only check files with the actual DbContext override — skip interfaces, XML docs, extension methods.
                // Match "protected override void OnModelCreating" at the start of a line (not in comments).
                if (!OnModelCreatingOverride().IsMatch(content))
                {
                    continue;
                }

                if (!content.Contains("ApplyGranitConventions", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every DbContext.OnModelCreating must call modelBuilder.ApplyGranitConventions(). " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Every concrete DbContext in <c>*.EntityFrameworkCore</c> or <c>*.Database</c> packages
    /// must either inherit from <c>GranitDbContext</c> (preferred) or carry the inline
    /// <c>ConfigureMultiTenantFilter</c> pattern (forced when single-inheritance already binds
    /// the type elsewhere — no context currently needs this). Calling the legacy
    /// <c>modelBuilder.ApplyGranitConventions(currentTenant, ...)</c> with a non-null tenant
    /// re-introduces the "frozen tenant" SQL leak fixed in #2129.
    /// </summary>
    [Fact]
    public void DbContext_classes_should_use_GranitDbContext_or_inline_parameterised_filter()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        // MigrationProgressDbContext is in *.EntityFrameworkCore.Migrations (different
        // project suffix) and doesn't carry tenant entities — already skipped by the
        // project filter, but listed here for explicit traceability.
        HashSet<string> exempted = ["MigrationProgressDbContext.cs", "GranitDbContext.cs"];

        List<string> violations = [];

        foreach (string project in Directory.GetDirectories(srcDir)
            .Where(d => Path.GetFileName(d).EndsWith(".EntityFrameworkCore", StringComparison.Ordinal)
                || Path.GetFileName(d).EndsWith(".Database", StringComparison.Ordinal)))
        {
            foreach (string csFile in Directory.GetFiles(project, "*DbContext.cs", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(csFile);
                if (fileName.StartsWith('I') || exempted.Contains(fileName))
                {
                    continue;
                }

                string content = File.ReadAllText(csFile);

                // Skip interfaces and abstract bases (not concrete DbContext classes).
                if (!content.Contains("sealed class", StringComparison.Ordinal))
                {
                    continue;
                }

                bool inheritsGranitDbContext = content.Contains(": GranitDbContext", StringComparison.Ordinal);
                bool implementsInlineFilter = content.Contains("ConfigureMultiTenantFilter", StringComparison.Ordinal);

                if (!inheritsGranitDbContext && !implementsInlineFilter)
                {
                    violations.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every concrete *DbContext.cs must inherit GranitDbContext (preferred) or replicate " +
            "its parameterised IMultiTenant filter inline (look for ConfigureMultiTenantFilter). " +
            "The legacy ApplyGranitConventions(currentTenant, ...) call inlines the tenant id as " +
            "a SQL literal — see PR #2129. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Test-project counterpart to <see cref="DbContext_classes_should_use_GranitDbContext_or_inline_parameterised_filter"/>,
    /// which only scans <c>src/</c>. A test harness that passes a non-null tenant to the legacy
    /// <c>ApplyGranitConventions(currentTenant, …)</c> overload re-introduces the frozen-tenant SQL
    /// leak (#2129) the moment it switches tenants across requests on a reused model — exactly the
    /// bug that kept <c>TenantIsolationTests</c> skipped until its harness was migrated to
    /// <c>GranitDbContext</c>. The allowlist holds contexts that exercise the legacy overload on
    /// purpose (unit tests of the convention itself, design-time stub parity) and only ever use a
    /// single tenant per model build, so they cannot freeze across tenants.
    /// </summary>
    [Fact]
    public void Test_DbContexts_should_not_pass_a_non_null_tenant_to_ApplyGranitConventions()
    {
        string testsDir = Path.Join(RepoRoot, "tests");

        HashSet<string> allowed =
        [
            "ModelBuilderExtensionsTests.cs",                 // unit tests OF the ApplyGranitConventions overloads
            "GranitDesignTimeTests.cs",                       // design-time stub parity (fixed tenant by design)
            "DbContextOptionsBuilderTestExtensionsTests.cs",  // unit test of a test helper, single tenant per build
        ];

        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(testsDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(csFile);

            // Cheap pre-filter before paying for a parse; skip the deliberate exemptions.
            if (!source.Contains("ApplyGranitConventions", StringComparison.Ordinal)
                || allowed.Contains(Path.GetFileName(csFile)))
            {
                continue;
            }

            CancellationToken ct = TestContext.Current.CancellationToken;
            Microsoft.CodeAnalysis.SyntaxNode root =
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source, path: csFile, cancellationToken: ct).GetRoot(ct);

            foreach (Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation
                in root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>())
            {
                if (IsApplyGranitConventionsCall(invocation) && PassesNonNullTenant(invocation))
                {
                    Microsoft.CodeAnalysis.FileLinePositionSpan loc = invocation.GetLocation().GetLineSpan();
                    violations.Add($"{Path.GetRelativePath(RepoRoot, csFile)}:{loc.StartLinePosition.Line + 1}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "A test DbContext passes a non-null tenant to the legacy ApplyGranitConventions(currentTenant, …) " +
            "overload, which constant-folds the tenant id into the cached model and leaks it across requests " +
            "(#2129). Inherit GranitDbContext instead — forward currentTenant to the base ctor and let it wire " +
            "the parameterised @ef_filter__CurrentTenantId filter. If the context genuinely unit-tests the legacy " +
            "overload on a single tenant, add its file name to the allowlist above with a justification. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>True when the invocation is a call to <c>ApplyGranitConventions</c>.</summary>
    private static bool IsApplyGranitConventionsCall(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax? name = invocation.Expression switch
        {
            Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax m => m.Name,
            Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax s => s,
            _ => null,
        };
        return name?.Identifier.ValueText == "ApplyGranitConventions";
    }

    /// <summary>
    /// True when the <c>currentTenant</c> argument is present and not the <c>null</c> literal —
    /// i.e. the legacy frozen-tenant path. The currentTenant value is the named
    /// <c>currentTenant:</c> argument when present, otherwise the first positional argument
    /// (a lone <c>dataFilter:</c> named argument leaves currentTenant defaulted to null).
    /// </summary>
    private static bool PassesNonNullTenant(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
    {
        Microsoft.CodeAnalysis.SeparatedSyntaxList<Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax> args =
            invocation.ArgumentList.Arguments;

        Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax? tenantArg =
            args.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == "currentTenant")
            ?? args.FirstOrDefault(a => a.NameColon is null);

        if (tenantArg is null)
        {
            return false;
        }

        return tenantArg.Expression is not Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax literal
            || literal.RawKind != (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression;
    }

    [Fact]
    public void No_manual_HasQueryFilter_in_entity_configurations()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            foreach (string csFile in Directory.GetFiles(efProject, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(csFile);

                if (content.Contains("HasQueryFilter", StringComparison.Ordinal))
                {
                    string rel = Path.GetRelativePath(RepoRoot, csFile);

                    // LocalIdentity cannot implement ISoftDeletable (incompatible with UserManager),
                    // so OpenIddict's model builder must register the soft-delete filter manually.
                    if (rel.Contains("OpenIddict", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Granit.Persistence.EntityFrameworkCore itself implements ApplyGranitConventions
                    // — it is the centralized filter registration, not a manual override.
                    if (rel.Contains("Granit.Persistence.EntityFrameworkCore", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    violations.Add(rel);
                }
            }
        }

        violations.ShouldBeEmpty(
            "Manual HasQueryFilter() is forbidden — ApplyGranitConventions handles all standard filters. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EfCore_projects_should_reference_GranitPersistence()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            string csproj = Directory.GetFiles(efProject, "*.csproj").FirstOrDefault()!;
            if (csproj is null)
            {
                continue;
            }

            string content = File.ReadAllText(csproj);

            if (!content.Contains("Granit.Persistence.EntityFrameworkCore", StringComparison.Ordinal))
            {
                violations.Add(Path.GetFileName(efProject));
            }
        }

        violations.ShouldBeEmpty(
            "Every *.EntityFrameworkCore project must reference Granit.Persistence (isolated DbContext pattern). " +
            $"Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EfCore_modules_should_DependOn_GranitPersistenceEntityFrameworkCoreModule()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            foreach (string csFile in Directory.GetFiles(efProject, "*Module.cs", SearchOption.TopDirectoryOnly))
            {
                string content = File.ReadAllText(csFile);

                if (!content.Contains(": GranitModule", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!content.Contains("GranitPersistenceEntityFrameworkCoreModule", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every *.EntityFrameworkCore module must have [DependsOn(typeof(GranitPersistenceEntityFrameworkCoreModule))]. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EfCore_extension_methods_should_use_interceptor_DI_pattern()
    {
        // Why Roslyn instead of grep: the previous plain-text scan tripped on any
        // file that *mentioned* "AddDbContextFactory" anywhere — xmldoc, comments,
        // string literals — and only required "ServiceLifetime.Scoped" to appear
        // somewhere in the same file, with no link between the two. False positives
        // (doc strings) and false negatives (a separate Scoped registration in the
        // same file masking a 1-arg overload call) were both possible. Parsing the
        // syntax tree lets us look at actual invocations only, and inspect their
        // arguments directly.
        string srcDir = Path.Join(RepoRoot, "src");

        // MigrationProgressDbContext bootstraps the migration runner before tenant
        // interceptors are available; intentionally registered without them.
        // Same exemption as the AddGranitDbContext / Configure*Module pairing test below.
        HashSet<string> exemptedContexts = ["MigrationProgressDbContext"];

        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            CancellationToken ct = TestContext.Current.CancellationToken;
            Microsoft.CodeAnalysis.SyntaxTree tree =
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(csFile), path: csFile, cancellationToken: ct);
            Microsoft.CodeAnalysis.SyntaxNode root = tree.GetRoot(ct);

            foreach (Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation
                in root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>())
            {
                if (!IsAddDbContextFactoryCall(invocation))
                {
                    continue;
                }

                if (TryGetTypeArgumentName(invocation) is string typeArg && exemptedContexts.Contains(typeArg))
                {
                    continue;
                }

                if (!UsesInterceptorAwareOverload(invocation))
                {
                    Microsoft.CodeAnalysis.FileLinePositionSpan loc = invocation.GetLocation().GetLineSpan();
                    violations.Add(
                        $"{Path.GetRelativePath(RepoRoot, csFile)}:{loc.StartLinePosition.Line + 1}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "AddDbContextFactory<TContext> must use the (IServiceProvider sp, DbContextOptionsBuilder opts) " +
            "configure overload so that AuditedEntityInterceptor / SoftDeleteInterceptor are resolved " +
            "from DI. Violators: " + string.Join(", ", violations));
    }

    /// <summary>
    /// True when the invocation is a call to <c>AddDbContextFactory</c> (with or without
    /// an explicit type argument list, on a member-access or a direct identifier).
    /// </summary>
    private static bool IsAddDbContextFactoryCall(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax? name = invocation.Expression switch
        {
            Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax m => m.Name,
            Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax s => s,
            _ => null,
        };
        return name?.Identifier.ValueText == "AddDbContextFactory";
    }

    /// <summary>
    /// Returns the bare type argument name (e.g. <c>"MigrationProgressDbContext"</c>) for
    /// invocations of the form <c>AddDbContextFactory&lt;TContext&gt;(...)</c>, or
    /// <see langword="null"/> when the type argument can't be statically read.
    /// </summary>
    private static string? TryGetTypeArgumentName(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax? name = invocation.Expression switch
        {
            Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax m => m.Name,
            Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax s => s,
            _ => null,
        };
        if (name is Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax g && g.TypeArgumentList.Arguments.Count == 1)
        {
            return g.TypeArgumentList.Arguments[0].ToString();
        }
        return null;
    }

    /// <summary>
    /// True when at least one argument is a lambda with two parameters
    /// (the <c>(sp, options) =&gt;</c> form). The other overloads accept either no configure
    /// callback at all or a single-parameter <c>options =&gt;</c> lambda — neither of which
    /// can resolve interceptors from DI.
    /// </summary>
    private static bool UsesInterceptorAwareOverload(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
    {
        foreach (Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax arg in invocation.ArgumentList.Arguments)
        {
            int paramCount = arg.Expression switch
            {
                Microsoft.CodeAnalysis.CSharp.Syntax.ParenthesizedLambdaExpressionSyntax p => p.ParameterList.Parameters.Count,
                Microsoft.CodeAnalysis.CSharp.Syntax.SimpleLambdaExpressionSyntax => 1,
                _ => -1,
            };
            if (paramCount == 2)
            {
                return true;
            }
        }
        return false;
    }

    [Fact]
    public void AddGranitDbContext_should_have_matching_ConfigureModule_extension()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        // MigrationProgressDbContext is a system context that bootstraps the migration
        // runner itself — it does not need a Configure*Module extension.
        HashSet<string> exempted = ["MigrationProgressDbContext"];

        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);

            foreach (Match match in AddGranitDbContextCall().Matches(content))
            {
                string contextType = match.Groups[1].Value;

                if (exempted.Contains(contextType))
                {
                    continue;
                }

                // Skip matches inside single-line comments (// ...). XML doc (///) is
                // already excluded by the regex lookbehind.
                int lineStart = content.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
                string lineUpToMatch = content[lineStart..match.Index];
                if (lineUpToMatch.Contains("//", StringComparison.Ordinal))
                {
                    continue;
                }

                // The DbContext's project must contain a Configure*Module() ModelBuilder
                // extension method so that host applications can include the module's
                // entity configurations in their own DbContext and generate migrations.
                string? projectDir = Path.GetDirectoryName(csFile);
                while (projectDir is not null && Directory.GetFiles(projectDir, "*.csproj").Length == 0)
                {
                    projectDir = Path.GetDirectoryName(projectDir);
                }

                if (projectDir is null)
                {
                    continue;
                }

                bool hasConfigureMethod = Directory.GetFiles(projectDir, "*ModelBuilderExtensions.cs", SearchOption.AllDirectories)
                    .Any(f =>
                    {
                        string ext = File.ReadAllText(f);
                        return ext.Contains("Configure", StringComparison.Ordinal)
                            && ext.Contains("this ModelBuilder", StringComparison.Ordinal);
                    });

                if (!hasConfigureMethod)
                {
                    string rel = Path.GetRelativePath(RepoRoot, csFile);
                    int line = content[..match.Index].Count(c => c == '\n') + 1;
                    violations.Add($"{rel}:{line} — AddGranitDbContext<{contextType}> without a matching Configure*Module() ModelBuilder extension");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every AddGranitDbContext<T> must have a Configure*Module() ModelBuilder extension " +
            "in the same project so host applications can include module tables in their migrations. " +
            $"Violators:\n  {string.Join("\n  ", violations)}");
    }

    private static IEnumerable<string> GetEfCoreProjectDirs(string srcDir) =>
        Directory.GetDirectories(srcDir)
            .Where(d => Path.GetFileName(d).EndsWith(".EntityFrameworkCore", StringComparison.Ordinal));

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(IsolatedDbContextTests).Assembly.Location);
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
    /// Matches the actual C# method override, not XML doc examples or comments.
    /// Requires whitespace before "protected" (indentation), excludes lines starting with "///".
    /// </summary>
    [GeneratedRegex(@"^\s+protected\s+override\s+void\s+OnModelCreating", RegexOptions.Multiline)]
    private static partial Regex OnModelCreatingOverride();

    /// <summary>
    /// Captures the type argument from <c>AddGranitDbContext&lt;SomeDbContext&gt;</c>
    /// or <c>AddGranitIsolatedDbContext&lt;SomeDbContext&gt;</c>.
    /// Excludes XML doc / comment lines (starts with whitespace + "///").
    /// </summary>
    [GeneratedRegex(@"(?<!///.*)\bAddGranit(?:Isolated)?DbContext<(\w+)>")]
    private static partial Regex AddGranitDbContextCall();
}
