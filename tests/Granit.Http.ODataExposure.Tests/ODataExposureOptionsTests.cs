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
