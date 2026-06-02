using ArchUnitNET.Domain;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable layered architecture rules: core/endpoints isolation from EF Core, IQueryable confinement,
/// domain entity leak prevention.
/// </summary>
public static class LayerDependencyRules
{
    /// <summary>
    /// Types in the given namespace prefix must not depend on EF Core.
    /// </summary>
    public static void TypesShouldNotDependOnEntityFrameworkCore(
        ArchUnitNET.Domain.Architecture architecture,
        string namespacePrefix,
        string layerDescription)
    {
        IEnumerable<IType> types = architecture.Types
            .Where(t => t.FullName.StartsWith(namespacePrefix, StringComparison.Ordinal));

        IEnumerable<IType> efCoreDeps = types
            .Where(t => t.Dependencies
                .Any(d => d.Target.FullName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)));

        efCoreDeps.ShouldBeEmpty(
            $"{layerDescription} must not depend on EF Core infrastructure. " +
            $"Violators: {string.Join(", ", efCoreDeps.Select(t => t.FullName))}");
    }

    /// <summary>
    /// Types whose namespace matches exactly (or is a child of) the given namespace,
    /// but excluding types from other modules that share a prefix
    /// (e.g. "Granit.Localization" excludes "Granit.Localization.EntityFrameworkCore").
    /// </summary>
    public static void ExactNamespaceShouldNotDependOnEntityFrameworkCore(
        ArchUnitNET.Domain.Architecture architecture,
        string exactNamespace,
        string layerDescription)
    {
        IEnumerable<IType> types = architecture.Types
            .Where(t =>
            {
                string ns = t.Namespace.FullName;
                return string.Equals(ns, exactNamespace, StringComparison.Ordinal)
                    || (ns.StartsWith(exactNamespace, StringComparison.Ordinal)
                        && ns.Length > exactNamespace.Length
                        && ns[exactNamespace.Length] == '.'
                        && !ns.Contains("EntityFrameworkCore", StringComparison.Ordinal));
            });

        IEnumerable<IType> efCoreDeps = types
            .Where(t => t.Dependencies
                .Any(d => d.Target.FullName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)));

        efCoreDeps.ShouldBeEmpty(
            $"{layerDescription} must not depend on EF Core infrastructure. " +
            $"Violators: {string.Join(", ", efCoreDeps.Select(t => t.FullName))}");
    }

    /// <summary>
    /// Endpoint types (namespace containing ".Endpoints") must not depend on EF Core.
    /// </summary>
    public static void EndpointTypesShouldNotDependOnEntityFrameworkCore(ArchUnitNET.Domain.Architecture architecture)
    {
        IEnumerable<IType> endpointTypes = architecture.Types
            .Where(t => t.Namespace.FullName.EndsWith(".Endpoints", StringComparison.Ordinal) ||
                        t.Namespace.FullName.Contains(".Endpoints.", StringComparison.Ordinal));

        IEnumerable<IType> efCoreDeps = endpointTypes
            .Where(t => t.Dependencies
                .Any(d => d.Target.FullName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)));

        efCoreDeps.ShouldBeEmpty(
            "Endpoints must use abstractions (ports), not EF Core directly. " +
            $"Violators: {string.Join(", ", efCoreDeps.Select(t => t.FullName))}");
    }

    /// <summary>
    /// Types in endpoint namespaces must not inherit from domain entity base classes.
    /// Endpoints may reference entities for internal mapping, but DTOs (Request/Response)
    /// must never extend Entity, AggregateRoot, etc.
    /// </summary>
    public static void EndpointTypesShouldNotInheritFromDomainEntities(
        ArchUnitNET.Domain.Architecture architecture,
        params string[] domainBaseClassFullNames)
    {
        HashSet<string> domainBaseClasses = domainBaseClassFullNames.ToHashSet(StringComparer.Ordinal);

        IEnumerable<Class> violations = architecture.Classes
            .Where(c => (c.Namespace.FullName.EndsWith(".Endpoints", StringComparison.Ordinal) ||
                         c.Namespace.FullName.Contains(".Endpoints.", StringComparison.Ordinal))
                && c.Dependencies
                    .Where(d => d is ArchUnitNET.Domain.Dependencies.InheritsBaseClassDependency)
                    .Any(d => domainBaseClasses.Contains(d.Target.FullName)));

        violations.ShouldBeEmpty(
            "Endpoint types must not inherit from domain entity base classes — use standalone Request/Response DTOs. " +
            $"Violators: {string.Join(", ", violations.Select(c => c.FullName))}");
    }

    /// <summary>
    /// Exception classes must not reside in endpoint namespaces.
    /// Exceptions are domain concerns and belong in Core, Domain, or module root packages.
    /// </summary>
    public static void ExceptionsShouldNotResideInEndpoints(ArchUnitNET.Domain.Architecture architecture)
    {
        IEnumerable<Class> violations = architecture.Classes
            .Where(c => (c.Namespace.FullName.EndsWith(".Endpoints", StringComparison.Ordinal) ||
                         c.Namespace.FullName.Contains(".Endpoints.", StringComparison.Ordinal))
                && c.Name.EndsWith("Exception", StringComparison.Ordinal));

        violations.ShouldBeEmpty(
            "Exception classes must not reside in Endpoints namespaces — they are domain concerns. " +
            $"Violators: {string.Join(", ", violations.Select(c => c.FullName))}");
    }

    /// <summary>
    /// Exception classes must not depend on ASP.NET Core types — exceptions are domain concerns.
    /// </summary>
    public static void ExceptionsShouldNotDependOnAspNetCore(
        ArchUnitNET.Domain.Architecture architecture,
        string typePrefix)
    {
        IEnumerable<Class> exceptionClasses = architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && c.Name.EndsWith("Exception", StringComparison.Ordinal));

        IEnumerable<Class> violations = exceptionClasses
            .Where(c => c.Dependencies
                .Any(d => d.Target.FullName.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)));

        violations.ShouldBeEmpty(
            "Exception classes must not depend on ASP.NET Core — they are domain concerns. " +
            $"Violators: {string.Join(", ", violations.Select(c => c.FullName))}");
    }

    /// <summary>
    /// IQueryable must not escape the persistence/data layer.
    /// Types whose namespace contains a default-allowed fragment (EntityFrameworkCore,
    /// QueryEngine, Persistence, Analytics, ODataExposure) are exempt — these
    /// modules ARE the query pipeline; surfacing <c>IQueryable</c> on a bridge
    /// type (e.g. the OData host-feed builder's user-supplied bypass lambda)
    /// is part of their contract. Types following the <c>*QueryableSource</c>
    /// convention — the standard bridge for exposing IQueryable to the
    /// QueryEngine endpoints layer — and concrete <c>*MetricDefinition</c> types
    /// deriving from <c>JoinedMetricDefinition&lt;TEntity, TJoined, TValue&gt;</c>,
    /// whose <c>Project</c> method intrinsically takes <c>IQueryable</c> on both
    /// sides of the join (the projection is the unit of aggregation; running it
    /// elsewhere would force materialisation in memory), are also exempt.
    /// </summary>
    public static void IQueryableShouldNotEscapePersistenceLayer(
        ArchUnitNET.Domain.Architecture architecture)
    {
        string[] allowedNamespaceFragments = ["EntityFrameworkCore", "QueryEngine", "Persistence", "Export", "Identity.Local", "Identity.OpenIddict", "Analytics", "ODataExposure"];

        IEnumerable<IType> violators = architecture.Types
            .Where(t => !allowedNamespaceFragments.Any(ns =>
                t.Namespace.FullName.Contains(ns, StringComparison.Ordinal)))
            .Where(t => !t.Name.EndsWith("QueryableSource", StringComparison.Ordinal))
            .Where(t => !t.Name.EndsWith("EndpointRouteBuilderExtensions", StringComparison.Ordinal))
            .Where(t => !t.Name.EndsWith("DataSource", StringComparison.Ordinal))
            .Where(t => !t.Name.EndsWith("MetricDefinition", StringComparison.Ordinal))
            .Where(t => t.Dependencies
                .Any(d => d.Target.FullName.StartsWith("System.Linq.IQueryable", StringComparison.Ordinal)));

        violators.ShouldBeEmpty(
            "IQueryable<T> must not escape the persistence layer. " +
            "Types following the *QueryableSource convention or *MetricDefinition (joined-metric Project hook) are exempt. " +
            $"Violators: {string.Join(", ", violators.Select(t => t.FullName))}");
    }

    /// <summary>
    /// Non-endpoint types (base domain, EF Core, BackgroundJobs) matching the given namespace prefix
    /// must not depend on ASP.NET transport namespaces. Types in <c>.Endpoints</c> namespaces are exempt.
    /// </summary>
    public static void NonEndpointTypesShouldNotDependOnAspNetTransport(
        ArchUnitNET.Domain.Architecture architecture,
        string namespacePrefix,
        string layerDescription)
    {
        string[] aspNetTransportNamespaces =
        [
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Routing",
            "Microsoft.AspNetCore.Builder",
            "Microsoft.AspNetCore.Mvc",
        ];

        IEnumerable<IType> nonEndpointTypes = architecture.Types
            .Where(t => t.Namespace.FullName.StartsWith(namespacePrefix, StringComparison.Ordinal))
            .Where(t => !t.Namespace.FullName.EndsWith(".Endpoints", StringComparison.Ordinal)
                && !t.Namespace.FullName.Contains(".Endpoints.", StringComparison.Ordinal));

        IEnumerable<IType> violators = nonEndpointTypes
            .Where(t => t.Dependencies
                .Any(d => aspNetTransportNamespaces.Any(ns =>
                    d.Target.FullName.StartsWith(ns, StringComparison.Ordinal))));

        violators.ShouldBeEmpty(
            $"{layerDescription} non-endpoint types (base, EF Core, BackgroundJobs) must not depend " +
            "on ASP.NET transport namespaces (Http, Routing, Builder, Mvc). " +
            $"Violators: {string.Join(", ", violators.Select(t => t.FullName))}");
    }
}
