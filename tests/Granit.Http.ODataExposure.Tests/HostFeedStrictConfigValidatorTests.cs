using Granit.Authorization;
using Granit.DataExchange.Export;
using Granit.Domain;
using Granit.Entities;
using Granit.Http.ODataExposure.Extensions;
using Granit.Http.ODataExposure.Options;
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
/// Host-feed strict-config gates locked at <c>MapGranitODataHostEndpoints</c>
/// time. Three rules on top of the tenant-feed validator: permission must
/// resolve to <c>MultiTenancySides.Host</c>, anonymous access is not
/// available, and <c>IMultiTenant</c> entities require an explicit
/// <c>AcknowledgeCrossTenantExposure</c> bypass lambda.
/// </summary>
public sealed class HostFeedStrictConfigValidatorTests
{
    [Fact]
    public void HostFeed_PermissionWithMultiTenancySidesTenant_Throws()
    {
        // Gate #1: host-feed sets must use a Host-only permission. Tenant or
        // Both permissions would let a tenant user be granted host-feed
        // access through tenant-side role inheritance.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                hostPermissions: new HostPermissionsProvider(
                    "OData.Test.Tenants.Read", MultiTenancySides.Tenant),
                configure: opts => opts.EntitySet<TenantStub, TenantStubQueryDefinition>("Tenants")
                    .RequirePermission("OData.Test.Tenants.Read")
                    .DisableExpand()));

        ex.Message.ShouldContain("MultiTenancySides.Tenant");
        ex.Message.ShouldContain("Tenants");
    }

    [Fact]
    public void HostFeed_PermissionWithMultiTenancySidesBoth_Throws()
    {
        // Both is also rejected — host-feed requires strict Host-side scope.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                hostPermissions: new HostPermissionsProvider(
                    "OData.Test.Tenants.Read", MultiTenancySides.Both),
                configure: opts => opts.EntitySet<TenantStub, TenantStubQueryDefinition>("Tenants")
                    .RequirePermission("OData.Test.Tenants.Read")
                    .DisableExpand()));

        ex.Message.ShouldContain("MultiTenancySides.Both");
    }

    [Fact]
    public void HostFeed_UnregisteredPermission_Throws()
    {
        // Gate #1 (lookup): a permission with no IPermissionDefinitionProvider
        // declaration is rejected. Defending against typos in the permission
        // string at the host's call site.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                hostPermissions: HostPermissionsProvider.Empty,
                configure: opts => opts.EntitySet<TenantStub, TenantStubQueryDefinition>("Tenants")
                    .RequirePermission("OData.Test.Typo")
                    .DisableExpand()));

        ex.Message.ShouldContain("OData.Test.Typo");
        ex.Message.ShouldContain("no PermissionDefinition");
    }

    [Fact]
    public void HostFeed_IMultiTenantEntity_WithoutAcknowledge_Throws()
    {
        // Gate #2: IMultiTenant entity exposed on host-feed without the
        // explicit bypass lambda. Without it, the framework filter
        // tenantId == currentTenant.Id returns no rows for tenantless callers
        // — fail-closed but confusing. Force the developer to write the
        // bypass at the call site.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                hostPermissions: new HostPermissionsProvider(
                    "OData.Test.Invoices.Read", MultiTenancySides.Host),
                configure: opts => opts.EntitySet<MultiTenantInvoice, MultiTenantInvoiceQueryDefinition>("Invoices")
                    .RequirePermission("OData.Test.Invoices.Read")
                    .DisableExpand()));

        ex.Message.ShouldContain("AcknowledgeCrossTenantExposure");
        ex.Message.ShouldContain("Invoices");
        ex.Message.ShouldContain("IMultiTenant");
    }

    [Fact]
    public void HostFeed_NonMultiTenantEntity_WithoutAcknowledge_DoesNotThrow()
    {
        // Non-IMultiTenant entities (e.g. the Tenant table itself) do not
        // need the bypass — the framework filter does not apply to them.
        Should.NotThrow(() =>
            CallMap(
                hostPermissions: new HostPermissionsProvider(
                    "OData.Test.Tenants.Read", MultiTenancySides.Host),
                configure: opts => opts.EntitySet<TenantStub, TenantStubQueryDefinition>("Tenants")
                    .RequirePermission("OData.Test.Tenants.Read")
                    .DisableExpand()));
    }

    [Fact]
    public void HostFeed_FullyConfigured_DoesNotThrow()
    {
        // Happy path: Host permission + IMultiTenant entity + bypass lambda + expand intent.
        Should.NotThrow(() =>
            CallMap(
                hostPermissions: new HostPermissionsProvider(
                    "OData.Test.Invoices.Read", MultiTenancySides.Host),
                configure: opts => opts.EntitySet<MultiTenantInvoice, MultiTenantInvoiceQueryDefinition>("InvoicesAllTenants")
                    .RequirePermission("OData.Test.Invoices.Read")
                    .AcknowledgeCrossTenantExposure(q => q)
                    .DisableExpand()));
    }

    [Fact]
    public void HostFeed_ErrorMessage_AccumulatesAllOffendingSets()
    {
        // Same accumulation behaviour as the tenant-feed validator — a single
        // error listing every misconfigured set, not a one-by-one drip-fix loop.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CallMap(
                hostPermissions: new HostPermissionsProvider(
                    "OData.Test.Invoices.Read", MultiTenancySides.Tenant),
                configure: opts =>
                {
                    opts.EntitySet<MultiTenantInvoice, MultiTenantInvoiceQueryDefinition>("Invoices")
                        .RequirePermission("OData.Test.Invoices.Read")  // wrong side
                        .DisableExpand();                                 // missing acknowledge
                    opts.EntitySet<TenantStub, TenantStubQueryDefinition>("Tenants")
                        .RequirePermission("OData.Test.Unknown")          // unregistered
                        .DisableExpand();
                }));

        ex.Message.ShouldContain("Invoices");
        ex.Message.ShouldContain("Tenants");
        ex.Message.ShouldContain("MultiTenancySides.Tenant");
        ex.Message.ShouldContain("OData.Test.Unknown");
    }

    private static void CallMap(
        IPermissionDefinitionManager hostPermissions,
        Action<ODataHostExposureOptions> configure)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton<IPermissionChecker>(Substitute.For<IPermissionChecker>());
        builder.Services.AddSingleton(Substitute.For<IQueryableSource<MultiTenantInvoice>>());
        builder.Services.AddSingleton(Substitute.For<IQueryableSource<TenantStub>>());
        builder.Services.AddSingleton(Substitute.For<IQueryEngine<MultiTenantInvoice>>());
        builder.Services.AddSingleton(Substitute.For<IQueryEngine<TenantStub>>());
        builder.Services.AddSingleton<QueryDefinition<MultiTenantInvoice>>(new MultiTenantInvoiceQueryDefinition());
        builder.Services.AddSingleton<QueryDefinition<TenantStub>>(new TenantStubQueryDefinition());

        // ADR-050 plumbing: every entity exposed on a host-feed needs an
        // EntityDefinition + Export pair. Registered minimally so the
        // strict-config validator gates land on host-feed-specific failures
        // (the focus of these tests), not on missing-EntityDefinition
        // failures (covered in EntityDefinitionStrictConfigTests).
        builder.Services.AddSingleton<IEntityDefinitionDescriptor>(new MultiTenantInvoiceEntityDefinition());
        builder.Services.AddSingleton<IEntityDefinitionDescriptor>(new TenantStubEntityDefinition());
        builder.Services.AddSingleton<IExportDefinitionDescriptor>(new MultiTenantInvoiceExportDefinition());
        builder.Services.AddSingleton<IExportDefinitionDescriptor>(new TenantStubExportDefinition());

        builder.Services.AddGranitODataExposure();
        builder.Services.AddSingleton(hostPermissions);

        builder.Services.AddGranitRateLimiting(o =>
        {
            o.Enabled = true;
            o.Policies[ODataExposureEndpointRouteBuilderExtensions.HostRateLimitPolicyName] =
                new RateLimitPolicyOptions
                {
                    PermitLimit = 1000,
                    Window = TimeSpan.FromMinutes(1),
                };
        });

        using WebApplication app = builder.Build();
        app.MapGranitODataHostEndpoints("/api/granit/odata/host", configure);
    }

    /// <summary>
    /// Minimal in-memory <see cref="IPermissionDefinitionManager"/> stub —
    /// the validator only calls <see cref="IPermissionDefinitionManager.Find"/>,
    /// so the rest of the surface returns empties.
    /// </summary>
    private sealed class HostPermissionsProvider : IPermissionDefinitionManager
    {
        public static HostPermissionsProvider Empty { get; } = new();

        private readonly Dictionary<string, PermissionDefinition> _definitions;

        public HostPermissionsProvider() => _definitions = [];

        public HostPermissionsProvider(string name, MultiTenancySides sides)
            : this() => _definitions[name] = new PermissionDefinition(name, DisplayName: null, GroupName: "Test", sides);

        public bool Exists(string name) => _definitions.ContainsKey(name);
        public PermissionDefinition? Find(string name) => _definitions.GetValueOrDefault(name);
        public IReadOnlyList<PermissionDefinition> GetAll() => [.. _definitions.Values];
        public IReadOnlyList<PermissionGroup> GetGroups() => [];
    }

    public sealed class TenantStub
    {
        public Guid Id { get; init; }
        public string Slug { get; init; } = string.Empty;
    }

    public sealed class TenantStubQueryDefinition : QueryDefinition<TenantStub>
    {
        public override string Name => "Test.Tenants";
        protected override void Configure(QueryDefinitionBuilder<TenantStub> builder) =>
            builder.Column(t => t.Slug, c => c.Filterable());
    }

    public sealed class MultiTenantInvoice : IMultiTenant
    {
        public Guid Id { get; init; }
        public Guid? TenantId { get; set; }
        public string Number { get; init; } = string.Empty;
    }

    public sealed class MultiTenantInvoiceQueryDefinition : QueryDefinition<MultiTenantInvoice>
    {
        public override string Name => "Test.Invoices";
        protected override void Configure(QueryDefinitionBuilder<MultiTenantInvoice> builder) =>
            builder.Column(i => i.Number, c => c.Filterable());
    }

    public sealed class MultiTenantInvoiceEntityDefinition : EntityDefinition<MultiTenantInvoice>
    {
        public override string Name => "Test.MultiTenantInvoice";
        protected override void Configure(EntityDefinitionBuilder<MultiTenantInvoice> builder) =>
            builder.Query<MultiTenantInvoiceQueryDefinition>().Export<MultiTenantInvoiceExportDefinition>();
    }

    public sealed class TenantStubEntityDefinition : EntityDefinition<TenantStub>
    {
        public override string Name => "Test.TenantStub";
        protected override void Configure(EntityDefinitionBuilder<TenantStub> builder) =>
            builder.Query<TenantStubQueryDefinition>().Export<TenantStubExportDefinition>();
    }

    public sealed class MultiTenantInvoiceExportDefinition : ExportDefinition<MultiTenantInvoice>
    {
        public override string Name => "Test.MultiTenantInvoiceExport";
        protected override void Configure(ExportDefinitionBuilder<MultiTenantInvoice> builder) =>
            builder.Field(i => i.Number);
    }

    public sealed class TenantStubExportDefinition : ExportDefinition<TenantStub>
    {
        public override string Name => "Test.TenantStubExport";
        protected override void Configure(ExportDefinitionBuilder<TenantStub> builder) =>
            builder.Field(t => t.Slug);
    }
}
