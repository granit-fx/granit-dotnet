using System.Reflection;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Golden-model harness (#3157, overhaul Phase 2 — epic #3143): every DbContext in
/// <c>src/</c> builds its EF Core model offline (Npgsql, no connection) and the full
/// <c>ToDebugString</c> is compared against a committed baseline under
/// <c>ModelSnapshots/</c>. The Phase 2 convention-engine rewrite (#3158/#3159) must keep
/// every snapshot bit-identical; any deliberate model change regenerates its baseline with:
/// <code>
/// GRANIT_MODEL_SNAPSHOTS=regen dotnet test tests/Granit.ArchitectureTests --filter ModelSnapshot
/// </code>
/// </summary>
[Collection(ModelSnapshotSerialGroup.Name)]
public sealed class ModelSnapshotTests
{
    private const string RegenEnvVar = "GRANIT_MODEL_SNAPSHOTS";

    /// <summary>
    /// Contexts that cannot be built by the harness, with the reason. Every entry must be
    /// justified — an unbuildable context escapes the Phase 2 equivalence guarantee.
    /// </summary>
    private static readonly Dictionary<string, string> Unbuildable = new(StringComparer.Ordinal)
    {
        // (empty — add "Full.Type.Name" => reason)
    };

    public static TheoryData<string> DiscoveredContexts()
    {
        TheoryData<string> data = [];
        foreach (Type type in EnumerateContextTypes())
        {
            if (!Unbuildable.ContainsKey(type.FullName!))
            {
                data.Add(type.FullName!);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DiscoveredContexts))]
    public void Model_matches_committed_snapshot(string contextTypeName)
    {
        Type contextType = EnumerateContextTypes().Single(t => t.FullName == contextTypeName);
        string snapshot = BuildSnapshot(contextType);
        string path = SnapshotPath(contextTypeName);

        if (string.Equals(
                Environment.GetEnvironmentVariable(RegenEnvVar), "regen", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, snapshot);
            return;
        }

        File.Exists(path).ShouldBeTrue(
            $"no committed baseline for {contextTypeName} — regenerate with "
            + $"{RegenEnvVar}=regen dotnet test tests/Granit.ArchitectureTests --filter ModelSnapshot");

        string baseline = Normalize(File.ReadAllText(path));
        snapshot.ShouldBe(baseline,
            $"the EF Core model of {contextTypeName} drifted from its committed baseline. "
            + "If the change is deliberate, regenerate with "
            + $"{RegenEnvVar}=regen dotnet test tests/Granit.ArchitectureTests --filter ModelSnapshot "
            + "and review the snapshot diff; if not, the conventions produced a different model.");
    }

    [Fact]
    public void Model_build_is_deterministic()
    {
        // One representative full double-build guards against non-deterministic
        // ToDebugString content (hash codes, unordered sets) invalidating the harness.
        Type contextType = EnumerateContextTypes()
            .First(t => typeof(GranitDbContext).IsAssignableFrom(t));

        BuildSnapshot(contextType).ShouldBe(BuildSnapshot(contextType));
    }

    [Fact]
    public void Every_unbuildable_exemption_still_exists()
    {
        HashSet<string> discovered = [.. EnumerateContextTypes().Select(t => t.FullName!)];
        List<string> stale = [.. Unbuildable.Keys.Where(name => !discovered.Contains(name))];

        stale.ShouldBeEmpty(
            $"exempt contexts no longer exist — remove from Unbuildable: {string.Join(", ", stale)}");
    }

    [Fact]
    public void No_orphan_snapshot_files()
    {
        string dir = Path.Combine(SnapshotsDirectory());
        if (!Directory.Exists(dir))
        {
            return;
        }

        HashSet<string> expected = [.. EnumerateContextTypes()
            .Where(t => !Unbuildable.ContainsKey(t.FullName!))
            .Select(t => $"{t.FullName}.snap")];

        List<string> orphans = [.. Directory.EnumerateFiles(dir, "*.snap")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => !expected.Contains(name))
            .Order(StringComparer.Ordinal)];

        orphans.ShouldBeEmpty(
            $"snapshot files without a matching context (renamed/deleted?): {string.Join(", ", orphans)}");
    }

    // ── Harness ─────────────────────────────────────────────────────────

    internal static string BuildSnapshot(Type contextType, bool freshInternalServices = false)
    {
        using DbContext context = CreateContext(contextType, freshInternalServices);

        // ToDebugString prints only the query-filter collection TYPE, not the expressions —
        // and the named filters are half the convention surface Phase 2 must keep identical.
        // Append every named filter's expression explicitly, deterministically ordered.
        System.Text.StringBuilder snapshot = new(
            context.Model.ToDebugString(MetadataDebugStringOptions.LongDefault, indent: 0));
        snapshot.AppendLine().AppendLine().AppendLine("Query filters:");

        foreach (Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType in context.Model
            .GetEntityTypes()
            .OrderBy(et => et.Name, StringComparer.Ordinal))
        {
            foreach (Microsoft.EntityFrameworkCore.Metadata.IQueryFilter filter in entityType.GetDeclaredQueryFilters()
                .OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                snapshot.Append("  ").Append(entityType.ShortName())
                    .Append('[').Append(filter.Key).Append("]: ")
                    .AppendLine(filter.Expression?.ToString() ?? "(null)");
            }
        }

        return Normalize(snapshot.ToString());
    }

    private static DbContext CreateContext(Type contextType, bool freshInternalServices = false)
    {
        var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(
            typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;
        optionsBuilder
            .UseNpgsql("Host=snapshot;Database=snapshot;Username=snapshot;Password=snapshot")
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        if (freshInternalServices)
        {
            // Parity dual-builds flip a process-global switch between two builds of the SAME
            // context type: a shared internal service provider would serve the first build's
            // cached model to the second. A fresh provider isolates each build.
            optionsBuilder.EnableServiceProviderCaching(false);
        }

        ConstructorInfo constructor = contextType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(c => c.GetParameters().Any(p => typeof(DbContextOptions).IsAssignableFrom(p.ParameterType)))
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        object?[] arguments = [.. constructor.GetParameters().Select(parameter =>
            TryResolveConstructorArgument(parameter, optionsBuilder.Options, out object? value)
                ? value
                : throw new InvalidOperationException(
                    $"{contextType.Name}: cannot resolve constructor parameter "
                    + $"'{parameter.ParameterType.Name} {parameter.Name}' — add the context to "
                    + "Unbuildable with a justification, or teach the harness the new dependency."))];

        return (DbContext)constructor.Invoke(arguments);
    }

    private static bool TryResolveConstructorArgument(
        ParameterInfo parameter, DbContextOptions options, out object? value)
    {
        if (typeof(DbContextOptions).IsAssignableFrom(parameter.ParameterType))
        {
            value = options;
            return true;
        }

        if (parameter.ParameterType == typeof(ICurrentTenant))
        {
            value = GranitDesignTime.CurrentTenant;
            return true;
        }

        if (parameter.ParameterType == typeof(IDataFilter))
        {
            value = GranitDesignTime.DataFilter;
            return true;
        }

        // Encrypted-column contexts (Identity, Privacy, Notifications*, …): the service only
        // feeds value converters, whose model shape is instance-independent — a passthrough
        // stub keeps those models inside the golden guarantee instead of exempting them.
        if (parameter.ParameterType == typeof(Granit.Encryption.IStringEncryptionService))
        {
            value = SnapshotStringEncryptionService.Instance;
            return true;
        }

        // IndexingDbContext's model is parameterised by host config; snapshot a canonical
        // deterministic shape (single Guid key set, default dictionary, no embeddings).
        if (parameter.ParameterType == typeof(Granit.Indexing.EntityFrameworkCore.IndexingDbContextSchema))
        {
            value = new Granit.Indexing.EntityFrameworkCore.IndexingDbContextSchema(
                [typeof(Guid)], "english");
            return true;
        }

        // Optional dependency (e.g. `IDataFilter? dataFilter = null`) — design-time default.
        if (parameter.HasDefaultValue)
        {
            value = parameter.DefaultValue;
            return true;
        }

        value = null;
        return false;
    }

    private sealed class SnapshotStringEncryptionService : Granit.Encryption.IStringEncryptionService
    {
        public static readonly SnapshotStringEncryptionService Instance = new();

        public string Encrypt(string plainText) => plainText;

        public string? Decrypt(string cipherText) => cipherText;
    }

    internal static IEnumerable<Type> EnumerateContextTypes() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "Granit.*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .Select(name => Assembly.Load(new AssemblyName(name!)))
            .SelectMany(GetLoadableTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(DbContext).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.OfType<Type>()];
        }
    }

    private static string SnapshotPath(string contextTypeName) =>
        Path.Combine(SnapshotsDirectory(), $"{contextTypeName}.snap");

    private static string SnapshotsDirectory() =>
        Path.Combine(
            ArchitectureTestHelpers.FindRepoRoot(),
            "tests", "Granit.ArchitectureTests", "ModelSnapshots");

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
}
