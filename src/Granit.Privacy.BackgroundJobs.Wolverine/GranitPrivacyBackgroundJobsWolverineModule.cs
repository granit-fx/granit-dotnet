using Granit.Modularity;
using Granit.Privacy.DataExport.Exceptions;
using Granit.Wolverine;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Granit.Privacy.BackgroundJobs.Wolverine;

/// <summary>
/// Wires the Wolverine retry-with-cooldown policy for the personal-data export
/// assembly background job. Transient
/// <see cref="PrivacyExportAssemblyException"/> retries at 1 min / 5 min / 15 min
/// before the message moves to the dead-letter queue; the mid-flight per-shard
/// checkpoint store ensures retries resume past the last committed shard rather
/// than re-streaming every fragment from zero.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a typed exception.</b> The retry policy only kicks in for
/// <c>PrivacyExportAssemblyException</c> — the assembly service wraps unexpected
/// failures in that type. Non-transient failures (forged HMAC, malformed event)
/// are intentionally NOT wrapped: they bubble up as <c>InvalidOperationException</c>
/// and fall through to Wolverine's default error path, which moves them straight
/// to the DLQ without burning retry budget on a non-recoverable problem.
/// </para>
/// <para>
/// <b>DLQ PII.</b> The job payload carries the saga's <c>ExportCompletedEto</c>
/// verbatim, including <c>ReceivedFragment.EntryPath</c> values which can encode
/// user-visible file names. Those fields are marked
/// <c>[SensitiveData(Level = Confidential, Mode = Hash)]</c> on the contract so
/// downstream sanitisers (audit log redaction, MCP output filter, GDPR export
/// redaction) treat them conservatively. Wolverine's dead-letter store remains
/// part of the trust boundary — operators with DB access can still observe the
/// raw payload; a Wolverine-level serializer pass that honours the attribute is
/// a separate framework concern tracked in P6.4.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitPrivacyBackgroundJobsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitPrivacyBackgroundJobsWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.ConfigureWolverine(opts =>
        {
            // Cooldown schedule mirrors the plan §5: a transient blob-storage or
            // multipart-upload failure typically clears within minutes, so the
            // total retry budget (≈ 21 minutes) is short enough that an operator
            // sees the DLQ entry the same day the assembly tripped.
            opts.OnException<PrivacyExportAssemblyException>()
                .RetryWithCooldown(
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15))
                .Then.MoveToErrorQueue();
        });
    }
}
