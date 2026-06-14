using System.Reflection;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces DbContext lifetime hygiene for query sources: any <see cref="IQueryableSource{TEntity}"/>
/// implementation that creates its own context via an injected <c>IDbContextFactory&lt;&gt;</c> MUST
/// implement <see cref="IAsyncDisposable"/> and <see cref="IDisposable"/> so the factory-created
/// context (and its pooled connection) is released when the scoped source is disposed.
/// </summary>
/// <remarks>
/// A factory-created context held in a field with no disposal leaks a DbContext / connection on
/// every resolution (the query engine resolves the source per request). Sources that instead inject
/// a DI-owned scoped DbContext directly (e.g. host-owned <c>IWorkflowDbContext</c>) are exempt — the
/// container owns that lifetime and they must NOT dispose it. Reference impl:
/// <c>EfNotificationPreferenceQueryableSource</c> (lazy create + dispose).
/// </remarks>
public sealed class QueryableSourceDisposalConventionTests
{
    [Fact]
    public void Factory_based_QueryableSource_implementations_must_be_disposable()
    {
        string outputDir = Path.GetDirectoryName(typeof(QueryableSourceDisposalConventionTests).Assembly.Location)!;

        Assembly[] granitAssemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests"))
            .Select(TryLoadAssembly)
            .Where(a => a is not null)
            .ToArray()!;

        List<string> violations = [];

        foreach (Assembly assembly in granitAssemblies)
        {
            Type[] sources;
            try
            {
                sources = [.. assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(ImplementsQueryableSource)
                    .Where(CreatesOwnContextViaFactory)];
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (Type source in sources)
            {
                bool disposable = typeof(IDisposable).IsAssignableFrom(source);
                bool asyncDisposable = typeof(IAsyncDisposable).IsAssignableFrom(source);
                if (disposable && asyncDisposable)
                {
                    continue;
                }

                string missing = (asyncDisposable, disposable) switch
                {
                    (false, false) => "IAsyncDisposable and IDisposable",
                    (false, true) => "IAsyncDisposable",
                    _ => "IDisposable",
                };

                violations.Add(
                    $"{assembly.GetName().Name}: {source.Name} injects an IDbContextFactory<> and creates " +
                    $"its own DbContext but does not implement {missing}. Create the context lazily and " +
                    "dispose it (mirror EfNotificationPreferenceQueryableSource).");
            }
        }

        violations.ShouldBeEmpty(
            "Factory-based IQueryableSource implementations must dispose the DbContext they create:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static bool ImplementsQueryableSource(Type type) =>
        type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryableSource<>));

    private static bool CreatesOwnContextViaFactory(Type type) =>
        type.GetConstructors().Any(ctor =>
            ctor.GetParameters().Any(p =>
                p.ParameterType.IsGenericType
                && p.ParameterType.GetGenericTypeDefinition() is { Name: "IDbContextFactory`1", Namespace: "Microsoft.EntityFrameworkCore" }));

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
