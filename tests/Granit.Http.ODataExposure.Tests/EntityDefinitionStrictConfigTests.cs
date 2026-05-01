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
/// ADR-050 strict-config gates. Locks the rule that every OData EntitySet
/// must have a registered <see cref="EntityDefinition{TEntity}"/> AND that
/// the EntityDefinition must reference an <see cref="ExportDefinition{TEntity}"/>
/// via <c>b.Export&lt;T&gt;()</c>. Failure paths are captured here as a
/// dedicated suite so the messaging on each missing piece stays clear.
/// </summary>
public sealed class EntityDefinitionStrictConfigTests
{
    [Fact]
    public void EntitySet_WithoutEntityDefinition_Throws()
    {
        // Showcase migration scenario: an entity is wired on the OData feed
        // before its EntityDefinition has been written. Per ADR-050, the
        // OData feed represents the application's Granit.Entities adoption
        // state — entities without an EntityDefinition cannot be exposed.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                registerEntityDefinitions: false,
                registerExports: true,
                configure: opts => opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
                    .RequirePermission("OData.Test.Invoices.Read")
                    .DisableExpand()));

        ex.Message.ShouldContain("Invoices");
        ex.Message.ShouldContain("no registered EntityDefinition");
        ex.Message.ShouldContain("ADR-050");
    }

    [Fact]
    public void EntitySet_WithEntityDefinitionMissingExport_Throws()
    {
        // The EntityDefinition exists but did not call b.Export<T>().
        // Per ADR-050, the OData EDM whitelist is derived from the
        // referenced Export — without it, there is no contract to apply.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                registerEntityDefinitions: true,
                registerExports: true,
                useExportlessEntityDefinition: true,
                configure: opts => opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
                    .RequirePermission("OData.Test.Invoices.Read")
                    .DisableExpand()));

        ex.Message.ShouldContain("Invoices");
        ex.Message.ShouldContain("does not declare a b.Export<T>() reference");
    }

    [Fact]
    public void EntitySet_WithEntityDefinitionButNoExportRegistered_Throws()
    {
        // The EntityDefinition references an Export but no
        // IExportDefinitionDescriptor is registered for the entity. Common
        // misconfiguration: forgetting AddExportDefinition<TEntity, TDef>()
        // in the host's Program.cs.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                registerEntityDefinitions: true,
                registerExports: false,
                configure: opts => opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
                    .RequirePermission("OData.Test.Invoices.Read")
                    .DisableExpand()));

        ex.Message.ShouldContain("Invoices");
        ex.Message.ShouldContain("AddExportDefinition");
    }

    [Fact]
    public void EntitySet_FullyConfigured_DoesNotThrow()
    {
        Should.NotThrow(() =>
            CallMap(
                registerEntityDefinitions: true,
                registerExports: true,
                configure: opts => opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
                    .RequirePermission("OData.Test.Invoices.Read")
                    .DisableExpand()));
    }

    private static void CallMap(
        bool registerEntityDefinitions,
        bool registerExports,
        Action<Options.ODataExposureOptions> configure,
        bool useExportlessEntityDefinition = false)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton<IPermissionChecker>(Substitute.For<IPermissionChecker>());
        builder.Services.AddSingleton(Substitute.For<IQueryableSource<Invoice>>());
        builder.Services.AddSingleton(Substitute.For<IQueryEngine<Invoice>>());
        builder.Services.AddSingleton<QueryDefinition<Invoice>>(new InvoiceQueryDefinition());

        if (registerEntityDefinitions)
        {
            IEntityDefinitionDescriptor entityDef = useExportlessEntityDefinition
                ? new InvoiceEntityDefinitionWithoutExport()
                : new InvoiceEntityDefinition();
            builder.Services.AddSingleton(entityDef);
        }

        if (registerExports)
        {
            builder.Services.AddSingleton<IExportDefinitionDescriptor>(new InvoiceExportDefinition());
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

    public sealed class InvoiceEntityDefinition : EntityDefinition<Invoice>
    {
        public override string Name => "Test.Invoice";
        protected override void Configure(EntityDefinitionBuilder<Invoice> builder) =>
            builder.Query<InvoiceQueryDefinition>().Export<InvoiceExportDefinition>();
    }

    /// <summary>EntityDefinition that intentionally omits the b.Export call — exercises gate #2.</summary>
    public sealed class InvoiceEntityDefinitionWithoutExport : EntityDefinition<Invoice>
    {
        public override string Name => "Test.InvoiceNoExport";
        protected override void Configure(EntityDefinitionBuilder<Invoice> builder) =>
            builder.Query<InvoiceQueryDefinition>();
    }

    public sealed class InvoiceExportDefinition : ExportDefinition<Invoice>
    {
        public override string Name => "Test.InvoiceExport";
        protected override void Configure(ExportDefinitionBuilder<Invoice> builder) =>
            builder.Field(i => i.Number);
    }
}
