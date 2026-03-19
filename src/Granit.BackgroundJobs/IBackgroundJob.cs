namespace Granit.BackgroundJobs;

/// <summary>
/// Marker interface for background job messages.
/// </summary>
/// <remarks>
/// All background job message types must implement this interface and be decorated
/// with <see cref="RecurringJobAttribute"/>. The architecture test enforces the
/// <c>*Job</c> suffix on all implementors.
/// <para>
/// Jobs live in a <c>Jobs/</c> folder within the owning module, alongside their handler:
/// <code>
/// src/Granit.{Module}/Jobs/
///   OrphanCleanupJob.cs       // [RecurringJob] sealed record ... : IBackgroundJob
///   OrphanCleanupHandler.cs   // static partial class with HandleAsync
/// </code>
/// </para>
/// <para>Naming convention: <c>*Job</c> suffix (e.g., <c>OrphanBlobCleanupJob</c>).</para>
/// <para>Job name format: <c>{module-kebab}-{action-kebab}</c> (e.g., <c>"blob-storage-orphan-cleanup"</c>).</para>
/// </remarks>
public interface IBackgroundJob;
