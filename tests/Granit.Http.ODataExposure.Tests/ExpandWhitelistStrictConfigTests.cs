using Granit.Authorization;
using Granit.DataExchange.Export;
using Granit.Entities;
using Granit.Http.ODataExposure.Extensions;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.RateLimiting.Extensions;
using Granit.RateLimiting.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests;

/// <summary>
/// #3005 strict-config gates for the recursive <c>$expand</c> whitelist.
/// Every dotted path declared in <c>ExpandWhitelist(...)</c> is walked at
/// startup: each segment must exist as a navigation-like property on the CLR
/// type it is declared on, and every navigation-target type reachable
/// through a whitelisted path must have a registered
/// <c>IExportDefinitionDescriptor</c> — its scalar fields become the
/// target's EDM whitelist (ADR-050 transitive closure). A path that fails
/// either gate is a startup error, not a lax runtime pass-through.
/// </summary>
public sealed class ExpandWhitelistStrictConfigTests
{
    [Fact]
    public void Path_UnknownNavigation_Throws()
    {
        // Pre-#3005, a whitelist entry naming a nonexistent navigation was
        // accepted at startup and produced a raw parser error per request —
        // the descriptor was dead config. It is now a startup failure.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(opts => Set(opts).ExpandWhitelist("Ghost")));

        ex.Message.ShouldContain("Ghost");
        ex.Message.ShouldContain("not a navigation property");
        ex.Message.ShouldContain(nameof(Invoice));
    }

    [Fact]
    public void Path_ScalarSegment_Throws()
    {
        // Number exists on Invoice but is a scalar — $expand only walks
        // navigations, so whitelisting it is a config bug.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(opts => Set(opts).ExpandWhitelist("Number")));

        ex.Message.ShouldContain("Number");
        ex.Message.ShouldContain("not a navigation property");
    }

    [Fact]
    public void Path_TargetWithoutExportDefinition_Throws()
    {
        // ADR-050: the Customer target type has no ExportDefinition — there
        // is no scalar whitelist to derive, so exposing it through $expand
        // would leak every public property into $metadata. Startup error
        // with the fix spelled out.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                opts => Set(opts).ExpandWhitelist("Customer"),
                registerCustomerExport: false));

        ex.Message.ShouldContain("Invoices");
        ex.Message.ShouldContain("'Customer'");
        ex.Message.ShouldContain(nameof(Customer));
        ex.Message.ShouldContain("ADR-050");
        ex.Message.ShouldContain("register an ExportDefinition");
        ex.Message.ShouldContain("or remove the path");
    }

    [Fact]
    public void NestedPath_SecondHopUnknownNavigation_Throws()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(opts => Set(opts).ExpandWhitelist("Customer.Ghost")));

        ex.Message.ShouldContain("Customer.Ghost");
        ex.Message.ShouldContain("Ghost");
        ex.Message.ShouldContain(nameof(Customer));
    }

    [Fact]
    public void NestedPath_SecondHopTargetWithoutExport_Throws()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                opts => Set(opts).ExpandWhitelist("Customer.Address"),
                registerAddressExport: false));

        ex.Message.ShouldContain("Customer.Address");
        ex.Message.ShouldContain(nameof(Address));
    }

    [Fact]
    public void NestedPath_FullyRegistered_DoesNotThrow()
        => Should.NotThrow(() =>
            CallMap(opts => Set(opts).ExpandWhitelist("Customer.Address").MaxExpansionDepth(2)));

    [Fact]
    public void CollectionNavigation_ResolvesElementType()
    {
        // Lines is List<Line> — the closure must validate the ELEMENT type's
        // export, not List<>. Line has an export registered → accepted.
        Should.NotThrow(() =>
            CallMap(opts => Set(opts).ExpandWhitelist("Lines")));
    }

    [Fact]
    public void Errors_AccumulateAcrossPaths()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(opts => Set(opts).ExpandWhitelist("Ghost", "Number")));

        ex.Message.ShouldContain("Ghost");
        ex.Message.ShouldContain("Number");
    }

    private static ODataExposureOptionsEntitySetShim Set(Options.ODataExposureOptions opts) =>
        new(opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices").AllowAnonymousAccess());

    /// <summary>Keeps the test bodies terse: forwards the two builder calls the suite varies.</summary>
    private sealed class ODataExposureOptionsEntitySetShim(Options.ODataEntitySetBuilder<Invoice> builder)
    {
        private readonly Options.ODataEntitySetBuilder<Invoice> _builder = builder;

        public ODataExposureOptionsEntitySetShim ExpandWhitelist(params string[] paths)
        {
            _builder.ExpandWhitelist(paths);
            return this;
        }

        public ODataExposureOptionsEntitySetShim MaxExpansionDepth(int depth)
        {
            _builder.MaxExpansionDepth(depth);
            return this;
        }
    }

    private static void CallMap(
        Action<Options.ODataExposureOptions> configure,
        bool registerCustomerExport = true,
        bool registerAddressExport = true)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton<IPermissionChecker>(Substitute.For<IPermissionChecker>());
        builder.Services.AddSingleton(Substitute.For<IQueryableSource<Invoice>>());
        builder.Services.AddSingleton(Substitute.For<IQueryEngine<Invoice>>());
        builder.Services.AddSingleton<QueryDefinition<Invoice>>(new InvoiceQueryDefinition());
        builder.Services.AddSingleton<IEntityDefinitionDescriptor>(new InvoiceEntityDefinition());
        builder.Services.AddSingleton<IExportDefinitionDescriptor>(new InvoiceExportDefinition());
        builder.Services.AddSingleton<IExportDefinitionDescriptor>(new LineExportDefinition());

        if (registerCustomerExport)
        {
            builder.Services.AddSingleton<IExportDefinitionDescriptor>(new CustomerExportDefinition());
        }

        if (registerAddressExport)
        {
            builder.Services.AddSingleton<IExportDefinitionDescriptor>(new AddressExportDefinition());
        }

        builder.Services.AddGranitODataExposure();
        builder.Services.AddGranitRateLimiting(o =>
        {
            o.Enabled = true;
            o.Policies["granit-odata"] = new RateLimitPolicyOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1),
            };
        });

        using WebApplication app = builder.Build();
        app.MapGranitODataEndpoints("/api/granit/odata", opts =>
        {
            opts.AllowAnonymousMetadata();
            configure(opts);
        });
    }

    public sealed class Invoice
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public Customer? Customer { get; init; }
        public List<Line> Lines { get; init; } = [];
    }

    public sealed class Customer
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public Address? Address { get; init; }
    }

    public sealed class Address
    {
        public Guid Id { get; init; }
        public string City { get; init; } = string.Empty;
    }

    public sealed class Line
    {
        public Guid Id { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    public sealed class InvoiceQueryDefinition : QueryDefinition<Invoice>
    {
        public override string Name => "Test.Invoices";
        protected override void Configure(QueryDefinitionBuilder<Invoice> builder) =>
            builder.Column(i => i.Number, c => c.Filterable());
    }

    public sealed class InvoiceEntityDefinition : EntityDefinition<Invoice>
    {
        public override string Name => "Test.Invoice";
        protected override void Configure(EntityDefinitionBuilder<Invoice> builder) =>
            builder.Query<InvoiceQueryDefinition>().Export<InvoiceExportDefinition>();
    }

    public sealed class InvoiceExportDefinition : ExportDefinition<Invoice>
    {
        public override string Name => "Test.InvoiceExport";
        protected override void Configure(ExportDefinitionBuilder<Invoice> builder) =>
            builder.Field(i => i.Number);
    }

    public sealed class CustomerExportDefinition : ExportDefinition<Customer>
    {
        public override string Name => "Test.CustomerExport";
        protected override void Configure(ExportDefinitionBuilder<Customer> builder) =>
            builder.Field(c => c.Name);
    }

    public sealed class AddressExportDefinition : ExportDefinition<Address>
    {
        public override string Name => "Test.AddressExport";
        protected override void Configure(ExportDefinitionBuilder<Address> builder) =>
            builder.Field(a => a.City);
    }

    public sealed class LineExportDefinition : ExportDefinition<Line>
    {
        public override string Name => "Test.LineExport";
        protected override void Configure(ExportDefinitionBuilder<Line> builder) =>
            builder.Field(l => l.Description);
    }
}
