using Granit.Hostnames.Domain;

namespace Granit.Hostnames.Contracts;

/// <summary>Possible outcomes from <see cref="IHostnameRegistrationService.RegisterAsync"/>.</summary>
public enum HostnameRegistrationOutcome
{
    /// <summary>The hostname was created successfully.</summary>
    Succeeded,

    /// <summary>Another owner has already registered the same hostname.</summary>
    HostAlreadyTaken,
}

/// <summary>Result returned by <see cref="IHostnameRegistrationService.RegisterAsync"/>.</summary>
/// <param name="Outcome">The registration outcome.</param>
/// <param name="Hostname">
/// The created <see cref="ManagedHostname"/> when <paramref name="Outcome"/> is
/// <see cref="HostnameRegistrationOutcome.Succeeded"/>; otherwise <c>null</c>.
/// </param>
public sealed record HostnameRegistrationResult(
    HostnameRegistrationOutcome Outcome,
    ManagedHostname? Hostname);

/// <summary>Possible outcomes from <see cref="IHostnameRegistrationService.RequestVerificationAsync"/>.</summary>
public enum RequestVerificationOutcome
{
    /// <summary>Verification was initiated or re-queued successfully.</summary>
    Succeeded,

    /// <summary>No hostname with the requested id exists.</summary>
    NotFound,

    /// <summary>
    /// The hostname is in <see cref="HostnameStatus.Pending"/> state and the platform ingress
    /// target is not configured — verification cannot be started.
    /// </summary>
    IngressNotConfigured,
}

/// <summary>Result returned by <see cref="IHostnameRegistrationService.RequestVerificationAsync"/>.</summary>
/// <param name="Outcome">The verification-request outcome.</param>
/// <param name="Hostname">
/// The updated <see cref="ManagedHostname"/> when <paramref name="Outcome"/> is
/// <see cref="RequestVerificationOutcome.Succeeded"/>; otherwise <c>null</c>.
/// </param>
public sealed record RequestVerificationResult(
    RequestVerificationOutcome Outcome,
    ManagedHostname? Hostname);

/// <summary>
/// Orchestrates hostname registration and verification-initiation, encapsulating the
/// token-minting, expected-DNS-record building, and the
/// Pending → BeginVerification / Active|Error → RequestRecheck branch. Consumers
/// (Endpoints, CMS admin, …) map the typed outcome to their protocol's error codes.
/// </summary>
public interface IHostnameRegistrationService
{
    /// <summary>
    /// Registers a new hostname for an owning resource. When the platform ingress target is
    /// configured the DNS challenge is minted immediately, so the caller receives the records
    /// the owner must configure without a second round-trip.
    /// </summary>
    /// <param name="host">Fully-qualified domain name (validated inside the service).</param>
    /// <param name="ownerType">Opaque owner-resource discriminator (e.g. <c>"cms.site"</c>).</param>
    /// <param name="ownerId">Owning resource identifier.</param>
    /// <param name="tenantId">Owning tenant; <c>null</c> for a global hostname.</param>
    /// <param name="isPrimary">Whether this is the owner's canonical hostname.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="HostnameRegistrationOutcome.Succeeded"/> with the created hostname, or
    /// <see cref="HostnameRegistrationOutcome.HostAlreadyTaken"/> when the host is already registered.
    /// </returns>
    Task<HostnameRegistrationResult> RegisterAsync(
        string host,
        string ownerType,
        Guid ownerId,
        Guid? tenantId = null,
        bool isPrimary = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates or re-queues DNS verification for an existing hostname.
    /// <list type="bullet">
    ///   <item>Pending → mints the challenge token and expected DNS records, then transitions to Verifying.</item>
    ///   <item>Active / Error → resets backoff and re-queues (RequestRecheck).</item>
    /// </list>
    /// </summary>
    /// <param name="id">Identifier of the hostname to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="RequestVerificationOutcome.Succeeded"/> with the updated hostname,
    /// <see cref="RequestVerificationOutcome.NotFound"/> when no such hostname exists, or
    /// <see cref="RequestVerificationOutcome.IngressNotConfigured"/> when the hostname is Pending
    /// and the platform ingress target is not set.
    /// </returns>
    Task<RequestVerificationResult> RequestVerificationAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
