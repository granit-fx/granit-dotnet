using System.Collections.Frozen;
using Granit.Http.ODataExposure.Extensions;
using Granit.Http.ODataExposure.Internal;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests;

/// <summary>
/// #3005 — pins the AST-based per-request <c>$expand</c> validator that
/// replaced the top-level-only string parser. The clause is parsed by the
/// real <see cref="ODataQueryOptionParser"/> against an EDM built by
/// <see cref="ODataEdmModelBuilder"/>, then walked recursively: every
/// expanded navigation is checked as a dotted path against the resolved
/// whitelist (whitelisted paths + prefixes, case-insensitive) and against
/// <c>MaxExpansionDepth</c>. <c>$levels</c> literals are normalised into
/// repeated path segments so <c>Parent($levels=2)</c> and
/// <c>Parent($expand=Parent)</c> hit identical gates.
/// </summary>
public sealed class ODataExpandAstValidationTests
{
    private static readonly IEdmModel Model = BuildModel();

    [Theory]
    [InlineData("Customer", new[] { "Customer" }, 1)]
    [InlineData("customer", new[] { "Customer" }, 1)]                       // request casing
    [InlineData("Customer", new[] { "customer" }, 1)]                       // whitelist casing
    [InlineData("Customer,Lines", new[] { "Customer", "Lines" }, 1)]
    [InlineData("Customer", new[] { "Customer.Address" }, 1)]               // prefix of a whitelisted path
    [InlineData("Customer($expand=Address)", new[] { "Customer.Address" }, 2)]
    [InlineData("Customer($expand=address)", new[] { "customer.ADDRESS" }, 2)]
    [InlineData("Parent($levels=2)", new[] { "Parent.Parent" }, 2)]         // $levels ≡ explicit nesting
    [InlineData("Parent($expand=Parent)", new[] { "Parent.Parent" }, 2)]
    public void Expand_Allowed(string expand, string[] whitelist, int maxDepth)
    {
        (ProblemHttpResult? rejection, string reason) = Validate(expand, whitelist, maxDepth);

        rejection.ShouldBeNull(reason);
        reason.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Lines", new[] { "Customer" }, 1, "Lines")]
    [InlineData("Customer,Lines", new[] { "Customer" }, 1, "Lines")]
    [InlineData("Customer($expand=Address)", new[] { "Customer" }, 2, "Customer.Address")]
    [InlineData("Parent($levels=2)", new[] { "Parent" }, 2, "Parent.Parent")]  // repetition not whitelisted
    public void Expand_NotWhitelisted(string expand, string[] whitelist, int maxDepth, string offendingPath)
    {
        (ProblemHttpResult? rejection, string reason) = Validate(expand, whitelist, maxDepth);

        rejection.ShouldNotBeNull();
        reason.ShouldBe("expand_not_whitelisted");
        rejection!.ProblemDetails.Detail.ShouldNotBeNull();
        rejection.ProblemDetails.Detail!.ShouldContain(offendingPath);
    }

    [Theory]
    [InlineData("Customer($expand=Address)", new[] { "Customer.Address" }, 1)]
    [InlineData("Parent($levels=2)", new[] { "Parent.Parent" }, 1)]
    [InlineData("Parent($levels=max)", new[] { "Parent.Parent" }, 2)]  // $levels=max always exceeds a finite cap
    public void Expand_DepthExceeded(string expand, string[] whitelist, int maxDepth)
    {
        (ProblemHttpResult? rejection, string reason) = Validate(expand, whitelist, maxDepth);

        rejection.ShouldNotBeNull();
        reason.ShouldBe("expand_depth_exceeded");
        rejection!.ProblemDetails.Detail.ShouldNotBeNull();
        rejection.ProblemDetails.Detail!.ShouldContain(maxDepth.ToString());
    }

    [Fact]
    public void DepthGate_RunsBeforeWhitelistGate()
    {
        // A path that is BOTH too deep and un-whitelisted reports the depth
        // problem — depth is the resource-protection gate, the whitelist is
        // the disclosure gate; the former is the more actionable signal for
        // a BI author iterating on nesting.
        (ProblemHttpResult? rejection, string reason) =
            Validate("Customer($expand=Address)", ["Customer"], maxDepth: 1);

        rejection.ShouldNotBeNull();
        reason.ShouldBe("expand_depth_exceeded");
    }

    [Fact]
    public void BuildAllowedExpandPaths_GeneratesPrefixes_CaseInsensitive()
    {
        FrozenSet<string> paths = ODataExposureEndpointRouteBuilderExtensions
            .BuildAllowedExpandPaths(["Customer.Address", "Lines"]);

        paths.ShouldContain("Customer");
        paths.ShouldContain("Customer.Address");
        paths.ShouldContain("Lines");
        paths.Contains("customer.address").ShouldBeTrue();
        paths.Contains("LINES").ShouldBeTrue();
        paths.Count.ShouldBe(3);
    }

    [Fact]
    public void BuildAllowedExpandPaths_NullOrBlankEntries_YieldEmptySet()
    {
        ODataExposureEndpointRouteBuilderExtensions.BuildAllowedExpandPaths(null).ShouldBeEmpty();
        ODataExposureEndpointRouteBuilderExtensions.BuildAllowedExpandPaths([]).ShouldBeEmpty();
        ODataExposureEndpointRouteBuilderExtensions.BuildAllowedExpandPaths([" ", ""]).ShouldBeEmpty();
    }

    private static (ProblemHttpResult? Rejection, string Reason) Validate(
        string expand,
        string[] whitelist,
        int maxDepth)
    {
        IEdmEntitySet entitySet = Model.EntityContainer.FindEntitySet("Invoices")!;
        ODataQueryOptionParser parser = new(
            Model,
            entitySet.EntityType,
            entitySet,
            new Dictionary<string, string> { ["$expand"] = expand })
        {
            Resolver = new ODataUriResolver { EnableCaseInsensitive = true },
        };

        SelectExpandClause clause = parser.ParseSelectAndExpand();

        ODataEntitySetDescriptor descriptor = new(
            "Invoices", typeof(Invoice), typeof(string), null,
            ExpandWhitelist: whitelist,
            MaxExpansionDepth: maxDepth);

        return ODataExposureEndpointRouteBuilderExtensions.ValidateExpandItems(
            clause.SelectedItems,
            prefix: null,
            depth: 0,
            descriptor,
            ODataExposureEndpointRouteBuilderExtensions.BuildAllowedExpandPaths(whitelist));
    }

    /// <summary>
    /// EDM deliberately broader than any single test's whitelist (Customer +
    /// Lines + self-referencing Parent all navigable) so the parser accepts
    /// the clause and the rejection provably comes from the AST validator,
    /// not from a missing EDM navigation.
    /// </summary>
    private static IEdmModel BuildModel()
    {
        ODataEntitySetDescriptor descriptor = new("Invoices", typeof(Invoice), typeof(string), null);

        Dictionary<Type, ODataEntityTypeWhitelist> whitelist = new()
        {
            [typeof(Invoice)] = new([nameof(Invoice.Number)], ["Customer", "Lines", "Parent"]),
            [typeof(Customer)] = new([nameof(Customer.Name)], ["Address"]),
            [typeof(Address)] = new([nameof(Address.City)], []),
            [typeof(Line)] = new([nameof(Line.Description)], []),
        };

        return ODataEdmModelBuilder.Build([descriptor], whitelist);
    }

    private sealed class Invoice
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public Customer? Customer { get; init; }
        public List<Line> Lines { get; init; } = [];
        public Invoice? Parent { get; init; }
    }

    private sealed class Customer
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public Address? Address { get; init; }
    }

    private sealed class Address
    {
        public Guid Id { get; init; }
        public string City { get; init; } = string.Empty;
    }

    private sealed class Line
    {
        public Guid Id { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
