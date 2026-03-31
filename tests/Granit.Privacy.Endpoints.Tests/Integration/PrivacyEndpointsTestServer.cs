using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using Granit.Events;
using Granit.Guids;
using Granit.Http.Cookies;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataExport;
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

    private const string AuthenticatedRole = "authenticated";
    private const string ExportRole = "export";
    private const string DeletionRole = "deletion";
    private const string PurposesReadRole = "purposes-read";
    private const string AgreementsReadRole = "agreements-read";
    private const string AgreementsCreateRole = "agreements-create";

    private readonly WebApplication _app;

    public HttpClient AuthenticatedClient { get; }
    public HttpClient AnonymousClient { get; }

    public ICurrentUserService CurrentUser { get; }
    public IExportRequestTrackerWriter ExportWriter { get; }
    public IExportRequestTrackerReader ExportReader { get; }
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
            .AddPolicy(PrivacyPermissions.Export.Execute,
                policy => policy.RequireRole(ExportRole))
            .AddPolicy(PrivacyPermissions.Deletion.Execute,
                policy => policy.RequireRole(DeletionRole))
            .AddPolicy(PrivacyPermissions.Purposes.Read,
                policy => policy.RequireRole(PurposesReadRole))
            .AddPolicy(PrivacyPermissions.Agreements.Read,
                policy => policy.RequireRole(AgreementsReadRole))
            .AddPolicy(PrivacyPermissions.Agreements.Create,
                policy => policy.RequireRole(AgreementsCreateRole));

        // Service mocks
        builder.Services.AddSingleton(currentUser);
        builder.Services.AddSingleton(exportWriter);
        builder.Services.AddSingleton(exportReader);
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

        // Validators from the Privacy.Endpoints assembly
        builder.Services.AddValidatorsFromAssemblyContaining<PrivacyEndpointsOptions>(
            ServiceLifetime.Singleton, includeInternalTypes: true);

        WebApplication app = builder.Build();
        app.MapGranitPrivacy();
        await app.StartAsync().ConfigureAwait(false);

        string allRoles = string.Join(",",
            AuthenticatedRole, ExportRole, DeletionRole,
            PurposesReadRole, AgreementsReadRole, AgreementsCreateRole);

        HttpClient authenticatedClient = app.GetTestClient();
        authenticatedClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, allRoles);

        HttpClient anonymousClient = app.GetTestClient();

        return new PrivacyEndpointsTestServer(
            app, authenticatedClient, anonymousClient,
            currentUser, exportWriter, exportReader,
            deletionWriter, deletionReader,
            documentRegistry, agreementChecker, agreementStoreReader, agreementStoreWriter,
            regulationResolver, optOutWriter, optOutReader, purposeRegistry,
            eventBus, guidGenerator, timeProvider, currentTenant,
            cookieManager, cookieRegistry);
    }

    public async ValueTask DisposeAsync()
    {
        AuthenticatedClient.Dispose();
        AnonymousClient.Dispose();
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Fake authentication handler that authenticates when the <c>X-Test-Roles</c> header
/// is present. The authenticated user has a fixed <c>sub</c> claim set to
/// <see cref="PrivacyEndpointsTestServer.TestUserId"/>.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        Claim[] claims =
        [
            new("sub", PrivacyEndpointsTestServer.TestUserId.ToString()),
            new(ClaimTypes.NameIdentifier, PrivacyEndpointsTestServer.TestUserId.ToString()),
            new(ClaimTypes.Name, "test-user"),
            .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
