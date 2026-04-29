using Granit.Authorization;
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
/// C6 (#1395) — strict-config validator. Locks the rule that every
/// EntitySet must explicitly declare its permission and <c>$expand</c>
/// intents. Implicit-anonymous and implicit-no-expand are rejected at
/// <c>MapGranitODataEndpoints</c> time so misconfiguration cannot reach
/// production. Fail-fast at startup is the equivalent of an architecture
/// test for a config surface that lives inside a closure (and is therefore
/// not statically reflectable).
/// </summary>
public sealed class StrictConfigValidatorTests
{
    [Fact]
    public void EntitySet_WithoutPermissionOrAnonymousAck_Throws()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(opts => opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices").DisableExpand()));

        ex.Message.ShouldContain("RequirePermission");
        ex.Message.ShouldContain("AllowAnonymousAccess");
        ex.Message.ShouldContain("Invoices");
    }

    [Fact]
    public void EntitySet_WithoutExpandConfiguration_Throws()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(opts => opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
                .RequirePermission("OData.Test.Invoices.Read")));

        ex.Message.ShouldContain("ExpandWhitelist");
        ex.Message.ShouldContain("DisableExpand");
        ex.Message.ShouldContain("Invoices");
    }

    [Fact]
    public void EntitySet_FullyConfigured_DoesNotThrow()
    {
        Should.NotThrow(() =>
            CallMap(opts => opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
                .RequirePermission("OData.Test.Invoices.Read")
                .DisableExpand()));
    }

    [Fact]
    public void EntitySet_AnonymousAndDisableExpand_DoesNotThrow()
    {
        // Public-feed scenario: countries / currencies / tenant-agnostic
        // reference data. Both opt-outs are explicit, validator accepts.
        Should.NotThrow(() =>
            CallMap(opts => opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
                .AllowAnonymousAccess()
                .DisableExpand()));
    }

    [Fact]
    public void EntitySet_ErrorMessage_ListsAllOffendingSetsAtOnce()
    {
        // Multi-set apps deserve a single error listing every misconfigured
        // EntitySet, not a one-by-one drip-fix loop. The validator
        // accumulates errors before throwing.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(opts =>
            {
                opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices");      // missing both
                opts.EntitySet<Customer, CustomerQueryDefinition>("Customers");   // missing both
            }));

        ex.Message.ShouldContain("Invoices");
        ex.Message.ShouldContain("Customers");
    }

    private static void CallMap(Action<Options.ODataExposureOptions> configure)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Stub services so the route handler's DI binding is satisfiable
        // — the validator runs BEFORE any of these are touched, but
        // MapGranitODataEndpoints fully wires the routes after validation
        // succeeds, so the DI graph must resolve.
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton<IPermissionChecker>(Substitute.For<IPermissionChecker>());
        builder.Services.AddSingleton(Substitute.For<IQueryableSource<Invoice>>());
        builder.Services.AddSingleton(Substitute.For<IQueryableSource<Customer>>());
        builder.Services.AddSingleton(Substitute.For<IQueryEngine<Invoice>>());
        builder.Services.AddSingleton(Substitute.For<IQueryEngine<Customer>>());
        builder.Services.AddSingleton<QueryDefinition<Invoice>>(new InvoiceQueryDefinition());
        builder.Services.AddSingleton<QueryDefinition<Customer>>(new CustomerQueryDefinition());

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
        app.MapGranitODataEndpoints("/api/granit/odata", configure);
    }

    public sealed class Invoice
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
    }

    public sealed class InvoiceQueryDefinition : QueryDefinition<Invoice>
    {
        public override string Name => "Test.Invoices";
        protected override void Configure(QueryDefinitionBuilder<Invoice> builder) =>
            builder.Column(i => i.Number, c => c.Filterable());
    }

    public sealed class Customer
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed class CustomerQueryDefinition : QueryDefinition<Customer>
    {
        public override string Name => "Test.Customers";
        protected override void Configure(QueryDefinitionBuilder<Customer> builder) =>
            builder.Column(c => c.Name, b => b.Filterable());
    }
}
