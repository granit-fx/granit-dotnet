using ArchUnitNET.Domain;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Pins the dependency direction around the central <c>Granit.Contacts</c> aggregate
/// (US #1248): downstream modules (Invoicing, Subscriptions, Payments, Tax,
/// CustomerBalance, Procurement, Crm, Hr) consume <c>Contact</c> and push role flags
/// via integration events, but the Contacts module must never depend on any of them.
/// Reversing this would re-introduce the cross-module-join anti-pattern that the hybrid
/// flag design exists to avoid.
/// </summary>
public sealed class ContactsDependencyDirectionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    /// <summary>
    /// Namespaces of consumer modules that <c>Granit.Contacts</c> must NOT depend on.
    /// Adding a new role-emitting module? Wire its handler in the consumer module, not
    /// in Contacts — and add the namespace to this list.
    /// </summary>
    private static readonly string[] DownstreamConsumerNamespaces =
    [
        "Granit.Invoicing",
        "Granit.Subscriptions",
        "Granit.Payments",
        "Granit.Tax",
        "Granit.CustomerBalance",
        "Granit.Procurement",
        "Granit.Crm",
        "Granit.Hr",
    ];

    [Fact]
    public void Contacts_module_must_not_depend_on_downstream_consumers()
    {
        IEnumerable<IType> contactsTypes = Architecture.Types.Where(t =>
            t.FullName.StartsWith("Granit.Contacts.", StringComparison.Ordinal)
            || string.Equals(t.Namespace.FullName, "Granit.Contacts", StringComparison.Ordinal)
            || t.Namespace.FullName.StartsWith("Granit.Contacts.", StringComparison.Ordinal));

        var violations = contactsTypes
            .SelectMany(t => t.Dependencies.Select(d => (Source: t, TargetFullName: d.Target.FullName)))
            .Where(pair => DownstreamConsumerNamespaces.Any(ns =>
                pair.TargetFullName.StartsWith(ns + ".", StringComparison.Ordinal)
                || string.Equals(pair.TargetFullName, ns, StringComparison.Ordinal)))
            .Select(pair => $"{pair.Source.FullName} -> {pair.TargetFullName}")
            .Distinct()
            .ToList();

        violations.ShouldBeEmpty(
            "Granit.Contacts must remain consumer-direction-only — downstream modules push role flags " +
            "via integration events, never the reverse. See docs/guide/contacts/role-handler-pattern.md.");
    }
}
