using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Granit.Documents.PublicLinks.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Documents.PublicLinks.Endpoints.Extensions;

/// <summary>
/// Registration helpers for the anonymous public-link rate limiter (F18.4).
/// </summary>
public static class DocumentsPublicLinksRateLimiterServiceCollectionExtensions
{
    /// <summary>
    /// Policy name applied by the anonymous redemption group. Hosts MUST register
    /// this policy via <see cref="AddGranitDocumentsPublicLinksRateLimiter"/> and
    /// add <c>UseRateLimiter()</c> to the request pipeline for the limit to be
    /// enforced. The endpoint metadata is harmless when the middleware is absent.
    /// </summary>
    public const string PolicyName = "granit-documents-public-links";

    /// <summary>
    /// Registers an ASP.NET Core rate limiter with a fixed-window
    /// policy named <see cref="PolicyName"/>, keyed on a hash of
    /// <c>(client IP, bearer token)</c>. The window is 1 minute and the permit limit
    /// is sourced from <see cref="GranitDocumentsPublicLinksOptions.RateLimitPerMinute"/>
    /// (default <c>60</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// Optional configuration root. When supplied the <see cref="GranitDocumentsPublicLinksOptions.SectionName"/>
    /// section is bound onto the options instance — hosts that already bound the
    /// options elsewhere may pass <c>null</c>.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Partition key:</b> the raw bearer token NEVER appears in the partition
    /// key. The key is the hex-encoded SHA-256 digest of <c>"{ip}|{token}"</c> —
    /// the hash never leaves the process but defending in depth is cheap and keeps
    /// memory dumps clean of cleartext tokens.
    /// </para>
    /// <para>
    /// <b>Rejection response:</b> the default middleware response is kept — a
    /// minimal <c>429 Too Many Requests</c> with a <c>Retry-After</c> header. No
    /// custom body so revoked / unknown / rate-limited tokens still cannot be
    /// distinguished by the rejection text.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitDocumentsPublicLinksRateLimiter(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configuration is not null)
        {
            services
                .AddOptions<GranitDocumentsPublicLinksOptions>()
                .Bind(configuration.GetSection(GranitDocumentsPublicLinksOptions.SectionName))
                .ValidateDataAnnotations();
        }

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.AddPolicy(PolicyName, httpContext =>
            {
                IOptionsMonitor<GranitDocumentsPublicLinksOptions> monitor =
                    httpContext.RequestServices
                        .GetRequiredService<IOptionsMonitor<GranitDocumentsPublicLinksOptions>>();
                int permits = Math.Max(1, monitor.CurrentValue.RateLimitPerMinute);

                string partitionKey = BuildPartitionKey(httpContext);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true,
                    });
            });
        });

        return services;
    }

    private static string BuildPartitionKey(HttpContext httpContext)
    {
        string ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string token = httpContext.GetRouteValue("token") as string ?? string.Empty;

        // SHA-256 is non-cryptographic here (no adversary controls inputs in a way
        // that benefits from collision); we use it purely to avoid retaining the
        // raw token in the limiter's partition table.
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{ip}|{token}"));
        return Convert.ToHexString(digest);
    }
}
