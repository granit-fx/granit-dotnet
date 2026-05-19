using Granit.Authorization;
using Granit.DataExchange.Export;
using Granit.Domain;
using Granit.Entities;
using Granit.Http.ODataExposure.Extensions;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.RateLimiting.Extensions;
using Granit.RateLimiting.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests;

/// <summary>
/// Regression: an entity legitimately exposed on BOTH the tenant-feed and the
/// host-feed (e.g. the canonical User aggregate per ADR-051 — tenant-scope read
/// for self-service, host-scope cross-tenant read for compliance) must not
/// collide on the globally-unique endpoint name registry. The tenant-feed name
/// stays <c>OData{Set}List</c> (no breaking change for existing apps); the
/// host-feed gets a <c>Host</c> segment.
/// </summary>
public sealed class EndpointNameCollisionTests
{
    [Fact]
    public void SameEntitySetName_OnBothFeeds_DoesNotCollide()
    {
        using WebApplication app = BuildApp();

        // Mount "Users" on both feeds — pre-fix, this threw
        // InvalidOperationException("Duplicate endpoint name 'ODataUsersList'…").
        app.MapGranitODataEndpoints("/api/v1/odata", opts =>
            opts.EntitySet<User, UserQueryDefinition>("Users")
                .RequirePermission("OData.Test.Users.Read")
                .DisableExpand());

        Should.NotThrow(() =>
            app.MapGranitODataHostEndpoints("/api/v1/odata/host", opts =>
                opts.EntitySet<User, UserQueryDefinition>("Users")
                    .RequirePermission("OData.Test.Users.Host.Read")
                    .AcknowledgeCrossTenantExposure(q => q)
                    .DisableExpand()));

        HashSet<string> endpointNames =
        [
            .. ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(s => s.Endpoints)
                .Select(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.EndpointNameMetadata>()?.EndpointName)
                .OfType<string>(),
        ];

        endpointNames.ShouldContain("ODataUsersList");
        endpointNames.ShouldContain("ODataHostUsersList");
    }

    private static WebApplication BuildApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton<IPermissionChecker>(Substitute.For<IPermissionChecker>());
        builder.Services.AddSingleton(Substitute.For<IQueryableSource<User>>());
        builder.Services.AddSingleton(Substitute.For<IQueryEngine<User>>());
        builder.Services.AddSingleton<QueryDefinition<User>>(new UserQueryDefinition());
        builder.Services.AddSingleton<IEntityDefinitionDescriptor>(new UserEntityDefinition());
        builder.Services.AddSingleton<IExportDefinitionDescriptor>(new UserExportDefinition());

        // Host-feed validator looks up the required permission and checks
        // MultiTenancySides.Host — stub a provider with the two permissions
        // referenced by the test.
        builder.Services.AddSingleton<IPermissionDefinitionManager>(
            new StubPermissionDefinitionManager
            {
                ["OData.Test.Users.Read"] = MultiTenancySides.Tenant,
                ["OData.Test.Users.Host.Read"] = MultiTenancySides.Host,
            });

        builder.Services.AddGranitODataExposure();
        builder.Services.AddGranitRateLimiting(o =>
        {
            o.Enabled = true;
            o.Policies[ODataExposureEndpointRouteBuilderExtensions.RateLimitPolicyName] =
                new RateLimitPolicyOptions { PermitLimit = 1000, Window = TimeSpan.FromMinutes(1) };
            o.Policies[ODataExposureEndpointRouteBuilderExtensions.HostRateLimitPolicyName] =
                new RateLimitPolicyOptions { PermitLimit = 1000, Window = TimeSpan.FromMinutes(1) };
        });

        return builder.Build();
    }

    private sealed class StubPermissionDefinitionManager : IPermissionDefinitionManager
    {
        private readonly Dictionary<string, PermissionDefinition> _definitions = [];

        public MultiTenancySides this[string name]
        {
            set => _definitions[name] = new PermissionDefinition(name, DisplayName: null, GroupName: "Test", value);
        }

        public bool Exists(string name) => _definitions.ContainsKey(name);
        public PermissionDefinition? Find(string name) => _definitions.GetValueOrDefault(name);
        public IReadOnlyList<PermissionDefinition> GetAll() => [.. _definitions.Values];
        public IReadOnlyList<PermissionGroup> GetGroups() => [];
    }

    public sealed class User : IMultiTenant
    {
        public Guid Id { get; init; }
        public Guid? TenantId { get; set; }
        public string Email { get; init; } = string.Empty;
    }

    public sealed class UserQueryDefinition : QueryDefinition<User>
    {
        public override string Name => "Test.Users";
        protected override void Configure(QueryDefinitionBuilder<User> builder) =>
            builder.Column(u => u.Email, c => c.Filterable());
    }

    public sealed class UserEntityDefinition : EntityDefinition<User>
    {
        public override string Name => "Test.User";
        protected override void Configure(EntityDefinitionBuilder<User> builder) =>
            builder.Query<UserQueryDefinition>().Export<UserExportDefinition>();
    }

    public sealed class UserExportDefinition : ExportDefinition<User>
    {
        public override string Name => "Test.UserExport";
        protected override void Configure(ExportDefinitionBuilder<User> builder) =>
            builder.Field(u => u.Email);
    }
}
