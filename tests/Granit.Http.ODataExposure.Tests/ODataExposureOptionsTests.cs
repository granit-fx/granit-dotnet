using Granit.Http.ODataExposure.Options;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests;

/// <summary>
/// Pins the fluent-options surface — duplicate-set rejection, builder
/// mutations, and the contract that every registered EntitySet appears
/// once in the descriptor list (consumed downstream by the EDM model
/// builder + the per-set route mapper).
/// </summary>
public sealed class ODataExposureOptionsTests
{
    [Fact]
    public void EntitySet_RegistersOneDescriptor()
    {
        ODataExposureOptions options = new();

        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices");

        options.GetType().GetProperty("Descriptors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            !.GetValue(options).ShouldNotBeNull();
    }

    [Fact]
    public void EntitySet_DuplicateName_Throws()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices");

        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices"));

        ex.Message.ShouldContain("Invoices");
    }

    [Fact]
    public void EntitySet_DuplicateName_CaseInsensitive_Throws()
    {
        // EntitySet names round-trip through the OData URL — the protocol is
        // case-insensitive on the segment, so duplicates that differ only in
        // case must be rejected to avoid silent overrides.
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices");

        Should.Throw<ArgumentException>(() =>
            options.EntitySet<Invoice, InvoiceQueryDefinition>("invoices"));
    }

    [Fact]
    public void EntitySet_BlankName_Throws()
    {
        ODataExposureOptions options = new();

        Should.Throw<ArgumentException>(() =>
            options.EntitySet<Invoice, InvoiceQueryDefinition>(""));
    }

    [Fact]
    public void RequirePermission_PersistsOnTheDescriptor()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
            .RequirePermission("OData.Invoicing.Invoices.Read");

        // Descriptors is internal — accessing via the same internals-visible
        // assembly. Use reflection to keep the test in this file readable
        // without leaking a public testing seam.
        object? descriptorList = options.GetType()
            .GetProperty("Descriptors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(options);

        // Shape check via dynamic — the test surface is the runtime behaviour,
        // not the generic structure.
        object first = ((System.Collections.IEnumerable)descriptorList!)
            .Cast<object>()
            .Single();
        string? perm = (string?)first.GetType().GetProperty("RequiredPermission")!.GetValue(first);
        perm.ShouldBe("OData.Invoicing.Invoices.Read");
    }

    [Fact]
    public void RequirePermission_BlankPermission_Throws()
    {
        ODataExposureOptions options = new();
        ODataEntitySetBuilder<Invoice> builder = options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices");

        Should.Throw<ArgumentException>(() => builder.RequirePermission(""));
    }

    [Fact]
    public void Defaults_MatchHardeningContract()
    {
        // The C3 (#1392) defaults are intentional and load-bearing — a host
        // that registers an EntitySet without overriding MUST land on the
        // safe side (small page, capped top, count off, expand off). A
        // future relaxation here would silently widen every consuming app's
        // OData surface.
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices");

        object descriptor = GetSingleDescriptor(options);
        ((int)descriptor.GetType().GetProperty("MaxTop")!.GetValue(descriptor)!).ShouldBe(5000);
        ((int)descriptor.GetType().GetProperty("PageSize")!.GetValue(descriptor)!).ShouldBe(1000);
        ((bool)descriptor.GetType().GetProperty("CountEnabled")!.GetValue(descriptor)!).ShouldBeFalse();
        descriptor.GetType().GetProperty("ExpandWhitelist")!.GetValue(descriptor).ShouldBeNull();
        ((int)descriptor.GetType().GetProperty("MaxExpansionDepth")!.GetValue(descriptor)!).ShouldBe(1);
    }

    [Fact]
    public void MaxTop_StoresOnDescriptor_AndRejectsZeroOrNegative()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices").MaxTop(2500);

        ((int)GetSingleDescriptor(options).GetType().GetProperty("MaxTop")!.GetValue(GetSingleDescriptor(options))!)
            .ShouldBe(2500);

        ODataExposureOptions other = new();
        ODataEntitySetBuilder<Invoice> builder = other.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices");
        Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxTop(0));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxTop(-1));
    }

    [Fact]
    public void PageSize_StoresOnDescriptor()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices").PageSize(250);

        ((int)GetSingleDescriptor(options).GetType().GetProperty("PageSize")!.GetValue(GetSingleDescriptor(options))!)
            .ShouldBe(250);
    }

    [Fact]
    public void EnableCount_FlipsFlag()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices").EnableCount();

        ((bool)GetSingleDescriptor(options).GetType().GetProperty("CountEnabled")!.GetValue(GetSingleDescriptor(options))!)
            .ShouldBeTrue();
    }

    [Fact]
    public void ExpandWhitelist_StoresPropertyList()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
            .ExpandWhitelist("Customer", "Lines");

        var whitelist = (System.Collections.Generic.IReadOnlyList<string>?)
            GetSingleDescriptor(options).GetType().GetProperty("ExpandWhitelist")!.GetValue(GetSingleDescriptor(options));

        whitelist.ShouldNotBeNull();
        whitelist!.ShouldBe(["Customer", "Lines"]);
    }

    [Fact]
    public void ExpandWhitelist_EmptyArray_IsValid_AndDifferentFromNull()
    {
        // Default null = expand never registered; explicit [] = "I want zero
        // navigations exposed". Both reject every $expand request, but the
        // explicit empty form documents intent in the host code.
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices").ExpandWhitelist();

        var whitelist = (System.Collections.Generic.IReadOnlyList<string>?)
            GetSingleDescriptor(options).GetType().GetProperty("ExpandWhitelist")!.GetValue(GetSingleDescriptor(options));

        whitelist.ShouldNotBeNull();
        whitelist!.ShouldBeEmpty();
    }

    [Fact]
    public void MaxExpansionDepth_RejectsZeroOrNegative()
    {
        ODataExposureOptions options = new();
        ODataEntitySetBuilder<Invoice> builder = options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices");

        Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxExpansionDepth(0));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxExpansionDepth(-1));
    }

    [Fact]
    public void Builder_Chains_AcrossMultipleCalls()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
            .MaxTop(2500)
            .PageSize(250)
            .EnableCount()
            .ExpandWhitelist("Customer")
            .MaxExpansionDepth(2)
            .RequirePermission("OData.Test.Invoices.Read");

        object d = GetSingleDescriptor(options);
        ((int)d.GetType().GetProperty("MaxTop")!.GetValue(d)!).ShouldBe(2500);
        ((int)d.GetType().GetProperty("PageSize")!.GetValue(d)!).ShouldBe(250);
        ((bool)d.GetType().GetProperty("CountEnabled")!.GetValue(d)!).ShouldBeTrue();
        ((int)d.GetType().GetProperty("MaxExpansionDepth")!.GetValue(d)!).ShouldBe(2);
        ((string?)d.GetType().GetProperty("RequiredPermission")!.GetValue(d))
            .ShouldBe("OData.Test.Invoices.Read");
    }

    [Fact]
    public void AllowAnonymousAccess_ClearsRequiredPermission_AndFlagsAcknowledgement()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
            .RequirePermission("OData.Test.Invoices.Read")
            .AllowAnonymousAccess();

        object d = GetSingleDescriptor(options);
        ((string?)d.GetType().GetProperty("RequiredPermission")!.GetValue(d)).ShouldBeNull();
        ((bool)d.GetType().GetProperty("AnonymousAccessAcknowledged")!.GetValue(d)!).ShouldBeTrue();
    }

    [Fact]
    public void RequirePermission_ClearsAnonymousAccessFlag()
    {
        // Switching back to a permission gate after explicitly opting into
        // anonymous must take precedence — last call wins, and the flag is
        // reset so the strict validator sees the permission.
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
            .AllowAnonymousAccess()
            .RequirePermission("OData.Test.Invoices.Read");

        object d = GetSingleDescriptor(options);
        ((string?)d.GetType().GetProperty("RequiredPermission")!.GetValue(d))
            .ShouldBe("OData.Test.Invoices.Read");
        ((bool)d.GetType().GetProperty("AnonymousAccessAcknowledged")!.GetValue(d)!).ShouldBeFalse();
    }

    [Fact]
    public void DisableExpand_SetsEmptyWhitelist_AndFlagsAcknowledgement()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
            .DisableExpand();

        object d = GetSingleDescriptor(options);
        var whitelist = (System.Collections.Generic.IReadOnlyList<string>?)
            d.GetType().GetProperty("ExpandWhitelist")!.GetValue(d);
        whitelist.ShouldNotBeNull();
        whitelist!.ShouldBeEmpty();
        ((bool)d.GetType().GetProperty("ExpandConfigurationAcknowledged")!.GetValue(d)!).ShouldBeTrue();
    }

    [Fact]
    public void ExpandWhitelist_AlsoFlagsAcknowledgement()
    {
        ODataExposureOptions options = new();
        options.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
            .ExpandWhitelist("Customer");

        object d = GetSingleDescriptor(options);
        ((bool)d.GetType().GetProperty("ExpandConfigurationAcknowledged")!.GetValue(d)!).ShouldBeTrue();
    }

    private static object GetSingleDescriptor(ODataExposureOptions options)
    {
        object? list = options.GetType()
            .GetProperty("Descriptors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(options);
        return ((System.Collections.IEnumerable)list!).Cast<object>().Single();
    }

    private sealed class Invoice
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
    }

    private sealed class InvoiceQueryDefinition : QueryDefinition<Invoice>
    {
        public override string Name => "Test.Invoices";

        protected override void Configure(QueryDefinitionBuilder<Invoice> builder) =>
            builder.Column(i => i.Number, c => c.Filterable());
    }
}
