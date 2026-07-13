using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using Granit.Events;
using Granit.Guids;
using Granit.Http.Cookies;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Endpoints.Extensions;
using Granit.Privacy.Endpoints.Options;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.Options;
using Granit.Privacy.OptOut;
using Granit.Privacy.ProcessingPurposes;
using Granit.Privacy.Regulations;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Granit.Privacy.Endpoints.Tests.Integration;

/// <summary>
/// Test server that boots a minimal ASP.NET Core application with mocked services
/// and the privacy endpoints registered via <see cref="PrivacyEndpointRouteBuilderExtensions.MapGranitPrivacy"/>.
/// </summary>
internal sealed class PrivacyEndpointsTestServer : IAsyncDisposable
{
    public static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    public static readonly DateTimeOffset FixedNow = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly WebApplication _app;

    public HttpClient AuthenticatedClient { get; }
    public HttpClient AnonymousClient { get; }

    public ICurrentUserService CurrentUser { get; }
    public IExportRequestTrackerWriter ExportWriter { get; }
    public IExportRequestTrackerReader ExportReader { get; }
    public IPrivacyExportAuditWriter AuditWriter { get; }
    public IPrivacyScopeResolver ScopeResolver { get; }
    public IPrivacySubjectValidator SubjectValidator { get; }
    public IDeletionRequestTrackerWriter DeletionWriter { get; }
    public IDeletionRequestTrackerReader DeletionReader { get; }
    public ILegalDocumentRegistry DocumentRegistry { get; }
    public ILegalAgreementChecker AgreementChecker { get; }
    public ILegalAgreementStoreReader AgreementStoreReader { get; }
    public ILegalAgreementStoreWriter AgreementStoreWriter { get; }
    public IPrivacyRegulationResolver RegulationResolver { get; }
    public IOptOutRecordWriter OptOutWriter { get; }
    public IOptOutRecordReader OptOutReader { get; }
    public IProcessingPurposeRegistry PurposeRegistry { get; }
    public IDistributedEventBus EventBus { get; }
    public IGuidGenerator GuidGenerator { get; }
    public TimeProvider TimeProvider { get; }
    public ICurrentTenant CurrentTenant { get; }
    public IGranitCookieManager CookieManager { get; }
    public ICookieRegistry CookieRegistry { get; }

    private PrivacyEndpointsTestServer(
        WebApplication app,
        HttpClient authenticatedClient,
        HttpClient anonymousClient,
        ICurrentUserService currentUser,
        IExportRequestTrackerWriter exportWriter,
        IExportRequestTrackerReader exportReader,
        IPrivacyExportAuditWriter auditWriter,
        IPrivacyScopeResolver scopeResolver,
        IPrivacySubjectValidator subjectValidator,
        IDeletionRequestTrackerWriter deletionWriter,
        IDeletionRequestTrackerReader deletionReader,
        ILegalDocumentRegistry documentRegistry,
        ILegalAgreementChecker agreementChecker,
        ILegalAgreementStoreReader agreementStoreReader,
        ILegalAgreementStoreWriter agreementStoreWriter,
        IPrivacyRegulationResolver regulationResolver,
        IOptOutRecordWriter optOutWriter,
        IOptOutRecordReader optOutReader,
        IProcessingPurposeRegistry purposeRegistry,
        IDistributedEventBus eventBus,
        IGuidGenerator guidGenerator,
        TimeProvider timeProvider,
        ICurrentTenant currentTenant,
        IGranitCookieManager cookieManager,
        ICookieRegistry cookieRegistry)
    {
        _app = app;
        AuthenticatedClient = authenticatedClient;
        AnonymousClient = anonymousClient;
        CurrentUser = currentUser;
        ExportWriter = exportWriter;
        ExportReader = exportReader;
        AuditWriter = auditWriter;
        ScopeResolver = scopeResolver;
        SubjectValidator = subjectValidator;
        DeletionWriter = deletionWriter;
        DeletionReader = deletionReader;
        DocumentRegistry = documentRegistry;
        AgreementChecker = agreementChecker;
        AgreementStoreReader = agreementStoreReader;
        AgreementStoreWriter = agreementStoreWriter;
        RegulationResolver = regulationResolver;
        OptOutWriter = optOutWriter;
        OptOutReader = optOutReader;
        PurposeRegistry = purposeRegistry;
        EventBus = eventBus;
        GuidGenerator = guidGenerator;
        TimeProvider = timeProvider;
        CurrentTenant = currentTenant;
        CookieManager = cookieManager;
        CookieRegistry = cookieRegistry;
    }

    public static async Task<PrivacyEndpointsTestServer> CreateAsync()
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns(TestUserId.ToString());
        currentUser.Email.Returns("test@example.com");

        IExportRequestTrackerWriter exportWriter = Substitute.For<IExportRequestTrackerWriter>();
        IExportRequestTrackerReader exportReader = Substitute.For<IExportRequestTrackerReader>();
        IPrivacyExportAuditWriter auditWriter = Substitute.For<IPrivacyExportAuditWriter>();
        IPrivacyScopeResolver scopeResolver = Substitute.For<IPrivacyScopeResolver>();
        scopeResolver.ListVisibleAsync(Arg.Any<PrivacyExportContext>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ProviderDescriptor>)[]);
        IPrivacySubjectValidator subjectValidator = Substitute.For<IPrivacySubjectValidator>();
        // Default: accept every subject (existing happy-path tests rely on the validator
        // not vetoing). Cross-tenant probe tests override per-subject with .Returns(false).
        subjectValidator.SubjectExistsInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        IDeletionRequestTrackerWriter deletionWriter = Substitute.For<IDeletionRequestTrackerWriter>();
        IDeletionRequestTrackerReader deletionReader = Substitute.For<IDeletionRequestTrackerReader>();
        ILegalDocumentRegistry documentRegistry = Substitute.For<ILegalDocumentRegistry>();
        ILegalAgreementChecker agreementChecker = Substitute.For<ILegalAgreementChecker>();
        ILegalAgreementStoreReader agreementStoreReader = Substitute.For<ILegalAgreementStoreReader>();
        ILegalAgreementStoreWriter agreementStoreWriter = Substitute.For<ILegalAgreementStoreWriter>();
        IPrivacyRegulationResolver regulationResolver = Substitute.For<IPrivacyRegulationResolver>();
        IOptOutRecordWriter optOutWriter = Substitute.For<IOptOutRecordWriter>();
        IOptOutRecordReader optOutReader = Substitute.For<IOptOutRecordReader>();
        IProcessingPurposeRegistry purposeRegistry = Substitute.For<IProcessingPurposeRegistry>();
        IDistributedEventBus eventBus = Substitute.For<IDistributedEventBus>();
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IGranitCookieManager cookieManager = Substitute.For<IGranitCookieManager>();
        ICookieRegistry cookieRegistry = Substitute.For<ICookieRegistry>();

        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(FixedNow);

        guidGenerator.Create().Returns(_ => Guid.NewGuid());
        currentTenant.IsAvailable.Returns(false);

        // Default: regulation resolver returns EU GDPR profile
        regulationResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(new PrivacyRegulationProfile
            {
                Regulation = PrivacyRegulation.EuGdpr,
                DisplayName = "EU General Data Protection Regulation",
                JurisdictionCode = "EU",
                ConsentModel = ConsentModel.OptIn,
                AvailableLegalBases = [LegalBasis.Consent],
                SubjectAccessRequestDays = 30,
                DefaultDeletionGracePeriodDays = 14,
                MaxDeletionGracePeriodDays = 30,
                CookieConsentModel = ConsentModel.OptIn,
            });

        // Default: no existing deletion requests
        deletionReader.GetByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DeletionRequestStatus>());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // Authentication
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        // Authorization policies matching the permission names used by the endpoints
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(PrivacyPermissions.Exports.Execute,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, PrivacyPermissions.Exports.Execute))
            .AddPolicy(PrivacyPermissions.Exports.ExecuteOnBehalfOf,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, PrivacyPermissions.Exports.ExecuteOnBehalfOf))
            .AddPolicy(PrivacyPermissions.Deletions.Execute,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, PrivacyPermissions.Deletions.Execute))
            .AddPolicy(PrivacyPermissions.Purposes.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, PrivacyPermissions.Purposes.Read))
            .AddPolicy(PrivacyPermissions.Agreements.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, PrivacyPermissions.Agreements.Read))
            .AddPolicy(PrivacyPermissions.Agreements.Create,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, PrivacyPermissions.Agreements.Create));

        // Service mocks
        builder.Services.AddSingleton(currentUser);
        builder.Services.AddSingleton(exportWriter);
        builder.Services.AddSingleton(exportReader);
        builder.Services.AddSingleton(auditWriter);
        // RequireGranitRateLimiting filter resolves TenantPartitionedRateLimiter; wire a
        // disabled instance (Enabled=false → CheckAsync returns null → filter passes through)
        // so integration tests don't exercise the Redis counter store.
        builder.Services.AddMetrics();
        builder.Services.AddSingleton<Granit.RateLimiting.Diagnostics.RateLimitingMetrics>();
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(
            new Granit.RateLimiting.Options.GranitRateLimitingOptions { Enabled = false }));
        builder.Services.AddSingleton(Substitute.For<Granit.RateLimiting.Abstractions.IRateLimitCounterStore>());
        builder.Services.AddSingleton(Substitute.For<Granit.RateLimiting.Abstractions.IRateLimitQuotaProvider>());
        builder.Services.AddSingleton<Granit.RateLimiting.TenantPartitionedRateLimiter>();
        builder.Services.AddSingleton(scopeResolver);
        builder.Services.AddSingleton(subjectValidator);
        builder.Services.AddSingleton(deletionWriter);
        builder.Services.AddSingleton(deletionReader);
        builder.Services.AddSingleton(documentRegistry);
        builder.Services.AddSingleton(agreementChecker);
        builder.Services.AddSingleton(agreementStoreReader);
        builder.Services.AddSingleton(agreementStoreWriter);
        builder.Services.AddSingleton(regulationResolver);
        builder.Services.AddSingleton(optOutWriter);
        builder.Services.AddSingleton(optOutReader);
        builder.Services.AddSingleton(purposeRegistry);
        builder.Services.AddSingleton(eventBus);
        builder.Services.AddSingleton(guidGenerator);
        builder.Services.AddSingleton(timeProvider);
        builder.Services.AddSingleton(currentTenant);
        builder.Services.AddSingleton(cookieManager);
        builder.Services.AddSingleton(cookieRegistry);

        // Options
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new PrivacyEndpointsOptions()));
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new GranitPrivacyOptions()));

        // Real metrics (needs a real IMeterFactory)
        builder.Services.AddMetrics();
        builder.Services.AddSingleton<PrivacyMetrics>();

        // Real domain orchestration services (composed from the mocks above) so the endpoint
        // handlers exercise the full handler -> service -> dependency path in integration tests.
        builder.Services.AddScoped<Granit.Privacy.DataDeletion.IPrivacyDeletionRequestService,
            Granit.Privacy.DataDeletion.Internal.PrivacyDeletionRequestService>();
        builder.Services.AddScoped<Granit.Privacy.DataExport.IPrivacyExportRequestService,
            Granit.Privacy.DataExport.Internal.PrivacyExportRequestService>();

        // Validators from the Privacy.Endpoints assembly
        builder.Services.AddValidatorsFromAssemblyContaining<PrivacyEndpointsOptions>(
            ServiceLifetime.Singleton, includeInternalTypes: true);

        WebApplication app = builder.Build();
        app.MapGranitPrivacy();
        await app.StartAsync().ConfigureAwait(false);

        string allPermissions = string.Join(",",
            PrivacyPermissions.Exports.Execute,
            PrivacyPermissions.Exports.ExecuteOnBehalfOf,
            PrivacyPermissions.Deletions.Execute,
            PrivacyPermissions.Purposes.Read,
            PrivacyPermissions.Agreements.Read,
            PrivacyPermissions.Agreements.Create);

        HttpClient authenticatedClient = app.GetTestClient();
        authenticatedClient.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, allPermissions);

        HttpClient anonymousClient = app.GetTestClient();

        return new PrivacyEndpointsTestServer(
            app, authenticatedClient, anonymousClient,
            currentUser, exportWriter, exportReader, auditWriter, scopeResolver, subjectValidator,
            deletionWriter, deletionReader,
            documentRegistry, agreementChecker, agreementStoreReader, agreementStoreWriter,
            regulationResolver, optOutWriter, optOutReader, purposeRegistry,
            eventBus, guidGenerator, timeProvider, currentTenant,
            cookieManager, cookieRegistry);
    }

    /// <summary>
    /// Creates a test client authenticated with exactly the supplied permission names
    /// (sent via <see cref="TestAuthHandler.PermissionsHeader"/>). Pass none to get an
    /// authenticated-but-unauthorized caller.
    /// </summary>
    public HttpClient CreateClientWithPermissions(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(",", permissions));
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        AuthenticatedClient.Dispose();
        AnonymousClient.Dispose();
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Fake authentication handler that authenticates when the <c>X-Test-Permissions</c>
/// header (or the legacy <c>X-Test-Roles</c> header) is present. The authenticated user
/// has a fixed <c>sub</c> claim set to <see cref="PrivacyEndpointsTestServer.TestUserId"/>.
/// </summary>
/// <remarks>
/// Authorization in Granit is permission-based: permission names sent via
/// <see cref="PermissionsHeader"/> become <see cref="PermissionClaimType"/> claims, which
/// permission policies satisfy with <c>RequireClaim(PermissionClaimType, permissionName)</c>.
/// Role claims are still emitted from <see cref="RolesHeader"/> but never gate authorization.
/// </remarks>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    /// <summary>Header carrying the comma-separated permission names granted to the test caller.</summary>
    public const string PermissionsHeader = "X-Test-Permissions";

    /// <summary>Claim type a permission name is emitted under; matched by <c>RequireClaim</c> in permission policies.</summary>
    public const string PermissionClaimType = "permission";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        bool hasRoles = Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader);
        bool hasPermissions = Request.Headers.TryGetValue(PermissionsHeader, out Microsoft.Extensions.Primitives.StringValues permsHeader);

        if (!hasRoles && !hasPermissions)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        string[] permissions = permsHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        Claim[] claims =
        [
            new("sub", PrivacyEndpointsTestServer.TestUserId.ToString()),
            new(ClaimTypes.NameIdentifier, PrivacyEndpointsTestServer.TestUserId.ToString()),
            new(ClaimTypes.Name, "test-user"),
            .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
            .. permissions.Select(p => new Claim(PermissionClaimType, p.Trim())),
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
