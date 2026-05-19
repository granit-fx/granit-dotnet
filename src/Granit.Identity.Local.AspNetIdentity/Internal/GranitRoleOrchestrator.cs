using System.Data.Common;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.Persistence.EntityFrameworkCore.SharedConnection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Coordinates the dual write between <see cref="GranitRole"/> (Identity DbContext) and
/// <see cref="RoleMetadata"/> (host DbContext that implements <c>IPermissionGrantDbContext</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two execution paths, selected per-call:
/// </para>
/// <list type="number">
/// <item>
/// <b>Atomic</b> — when both <see cref="IIdentityDbContextAccessor"/> and
/// <see cref="IAuthorizationHostDbContextAccessor"/> are registered, both contexts are
/// relational, and their connection strings are equivalent. A single EF Core transaction
/// wraps both writes via <c>Database.OpenConnectionAsync</c> + <c>SetDbConnection</c> on a
/// fresh host context + <c>BeginTransactionAsync</c> / <c>UseTransactionAsync</c>, all inside
/// an <see cref="IExecutionStrategy"/> so the Npgsql / SQL Server retry-on-failure policy
/// still applies.
/// </item>
/// <item>
/// <b>Compensating</b> (fallback) — creates the <see cref="GranitRole"/> first, then the
/// <see cref="RoleMetadata"/>; on metadata failure, deletes the orphaned role so the
/// invariant "every Granit-managed role has matching metadata" converges. Used when the
/// accessors aren't wired (e.g., applications without Granit.OpenIddict) or when the two
/// DbContexts target different physical databases.
/// </item>
/// </list>
/// </remarks>
internal sealed partial class GranitRoleOrchestrator(
    RoleManager<GranitRole> roleManager,
    IRoleMetadataStore roleMetadataStore,
    IGuidGenerator guidGenerator,
    ILogger<GranitRoleOrchestrator> logger,
    IIdentityDbContextAccessor? identityDbContextAccessor = null,
    IAuthorizationHostDbContextAccessor? authorizationHostDbContextAccessor = null) : IGranitRoleOrchestrator
{
    /// <inheritdoc />
    public async Task<RoleMetadata> CreateAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        AtomicResolution atomic = await TryResolveAtomicContextsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (atomic.IsAtomic)
        {
            await using (atomic.HostContext)
            {
                return await ExecuteAtomicCreateAsync(
                    atomic.IdentityContext!, atomic.HostContext!, command, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return await ExecuteCompensatingCreateAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RoleMetadata> RenameAsync(
        Guid roleId,
        string newName,
        string? newDescription,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        AtomicResolution atomic = await TryResolveAtomicContextsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (atomic.IsAtomic)
        {
            await using (atomic.HostContext)
            {
                return await ExecuteAtomicRenameAsync(
                    atomic.IdentityContext!, atomic.HostContext!, roleId, newName, newDescription, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return await ExecuteCompensatingRenameAsync(roleId, newName, newDescription, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        AtomicResolution atomic = await TryResolveAtomicContextsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (atomic.IsAtomic)
        {
            await using (atomic.HostContext)
            {
                await ExecuteAtomicDeleteAsync(
                    atomic.IdentityContext!, atomic.HostContext!, roleId, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        await ExecuteCompensatingDeleteAsync(roleId, cancellationToken).ConfigureAwait(false);
    }

    // ────────────────────────────────────────────────────────────────────
    // Atomic path selection
    // ────────────────────────────────────────────────────────────────────

    private async Task<AtomicResolution> TryResolveAtomicContextsAsync(CancellationToken cancellationToken)
    {
        if (identityDbContextAccessor is null || authorizationHostDbContextAccessor is null)
        {
            LogAtomicPathUnavailable(logger, "accessor not registered");
            return AtomicResolution.NotAtomic;
        }

        DbContext idCtx = identityDbContextAccessor.DbContext;
        DbContext hostCtx;
        try
        {
            hostCtx = await authorizationHostDbContextAccessor
                .CreateFreshDbContextAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAtomicPathUnavailable(logger, $"host factory failed: {ex.GetType().Name}");
            return AtomicResolution.NotAtomic;
        }

        if (!idCtx.Database.IsRelational() || !hostCtx.Database.IsRelational())
        {
            await hostCtx.DisposeAsync().ConfigureAwait(false);
            LogAtomicPathUnavailable(logger, "provider is not relational");
            return AtomicResolution.NotAtomic;
        }

        if (!ConnectionStringsEquivalent(idCtx, hostCtx))
        {
            await hostCtx.DisposeAsync().ConfigureAwait(false);
            LogAtomicPathUnavailable(logger, "connection strings diverge");
            return AtomicResolution.NotAtomic;
        }

        LogAtomicPathSelected(logger);
        return new AtomicResolution(true, idCtx, hostCtx);
    }

    // ADO.NET-agnostic semantic comparison: DbConnectionStringBuilder parses
    // key/value pairs regardless of order and ignores whitespace. Works for
    // Npgsql, SQL Server, SQLite, MySQL, any provider that stores its
    // configuration in a connection string.
    private static bool ConnectionStringsEquivalent(DbContext a, DbContext b)
    {
        try
        {
            DbConnectionStringBuilder aBuilder = new()
            {
                ConnectionString = a.Database.GetDbConnection().ConnectionString,
            };
            DbConnectionStringBuilder bBuilder = new()
            {
                ConnectionString = b.Database.GetDbConnection().ConnectionString,
            };
            return aBuilder.EquivalentTo(bBuilder);
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Atomic path — shared connection, single transaction
    // ────────────────────────────────────────────────────────────────────

    private async Task<RoleMetadata> ExecuteAtomicCreateAsync(
        DbContext idCtx,
        DbContext hostCtx,
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        Guid roleId = guidGenerator.Create();
        RoleMetadata? created = null;

        IExecutionStrategy strategy = idCtx.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            idCtx.ChangeTracker.Clear();
            hostCtx.ChangeTracker.Clear();

            await idCtx.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                hostCtx.Database.SetDbConnection(idCtx.Database.GetDbConnection());
                await using IDbContextTransaction tx = await idCtx.Database
                    .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await hostCtx.Database.UseTransactionAsync(tx.GetDbTransaction(), cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    GranitRole granitRole = new()
                    {
                        Id = roleId,
                        Name = command.Name,
                        Description = command.Description,
                    };
                    IdentityResult createResult = await roleManager.CreateAsync(granitRole)
                        .ConfigureAwait(false);
                    if (!createResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create GranitRole '{command.Name}': " +
                            string.Join("; ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
                    }

                    var metadata = RoleMetadata.Create(
                        roleId,
                        command.Name,
                        command.MultiTenancySides,
                        command.TenantId,
                        command.ClientId,
                        command.Description,
                        command.IsSystem);
                    hostCtx.Set<RoleMetadata>().Add(metadata);
                    await hostCtx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                    created = metadata;
                }
                catch
                {
                    LogAtomicTransactionRolledBack(logger, roleId, command.Name);
                    await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                await idCtx.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }).ConfigureAwait(false);

        return created!;
    }

    private async Task<RoleMetadata> ExecuteAtomicRenameAsync(
        DbContext idCtx,
        DbContext hostCtx,
        Guid roleId,
        string newName,
        string? newDescription,
        CancellationToken cancellationToken)
    {
        RoleMetadata? updated = null;

        IExecutionStrategy strategy = idCtx.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            idCtx.ChangeTracker.Clear();
            hostCtx.ChangeTracker.Clear();

            await idCtx.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                hostCtx.Database.SetDbConnection(idCtx.Database.GetDbConnection());
                await using IDbContextTransaction tx = await idCtx.Database
                    .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await hostCtx.Database.UseTransactionAsync(tx.GetDbTransaction(), cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    RoleMetadata metadata = await hostCtx.Set<RoleMetadata>()
                        .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
                        .ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"No RoleMetadata found for id {roleId:D}.");

                    if (metadata.IsSystem)
                    {
                        throw new InvalidOperationException(
                            $"Role '{metadata.Name}' is a system role and cannot be renamed.");
                    }

                    GranitRole granitRole = await roleManager.FindByIdAsync(roleId.ToString("D"))
                        .ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"No GranitRole found for id {roleId:D}.");

                    granitRole.Name = newName;
                    granitRole.Description = newDescription;
                    IdentityResult updateResult = await roleManager.UpdateAsync(granitRole)
                        .ConfigureAwait(false);
                    if (!updateResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to update GranitRole '{roleId:D}': " +
                            string.Join("; ", updateResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
                    }

                    metadata.Rename(newName, newDescription);
                    await hostCtx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                    updated = metadata;
                }
                catch
                {
                    LogAtomicTransactionRolledBack(logger, roleId, newName);
                    await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                await idCtx.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }).ConfigureAwait(false);

        return updated!;
    }

    private async Task ExecuteAtomicDeleteAsync(
        DbContext idCtx,
        DbContext hostCtx,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = idCtx.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            idCtx.ChangeTracker.Clear();
            hostCtx.ChangeTracker.Clear();

            await idCtx.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                hostCtx.Database.SetDbConnection(idCtx.Database.GetDbConnection());
                await using IDbContextTransaction tx = await idCtx.Database
                    .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await hostCtx.Database.UseTransactionAsync(tx.GetDbTransaction(), cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    RoleMetadata metadata = await hostCtx.Set<RoleMetadata>()
                        .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
                        .ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"No RoleMetadata found for id {roleId:D}.");

                    if (metadata.IsSystem)
                    {
                        throw new InvalidOperationException(
                            $"Role '{metadata.Name}' is a system role and cannot be deleted.");
                    }

                    GranitRole? granitRole = await roleManager.FindByIdAsync(roleId.ToString("D"))
                        .ConfigureAwait(false);

                    metadata.MarkAsDeleted();
                    hostCtx.Set<RoleMetadata>().Remove(metadata);
                    await hostCtx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    if (granitRole is not null)
                    {
                        IdentityResult deleteResult = await roleManager.DeleteAsync(granitRole)
                            .ConfigureAwait(false);
                        if (!deleteResult.Succeeded)
                        {
                            throw new InvalidOperationException(
                                $"Failed to delete GranitRole '{metadata.Name}': " +
                                string.Join("; ", deleteResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
                        }
                    }

                    await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    LogAtomicTransactionRolledBack(logger, roleId, roleName: null);
                    await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                await idCtx.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    // ────────────────────────────────────────────────────────────────────
    // Compensating-write path — original Phase 1 behaviour, preserved verbatim
    // ────────────────────────────────────────────────────────────────────

    private async Task<RoleMetadata> ExecuteCompensatingCreateAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        Guid roleId = guidGenerator.Create();
        GranitRole granitRole = new()
        {
            Id = roleId,
            Name = command.Name,
            Description = command.Description,
        };

        IdentityResult createResult = await roleManager.CreateAsync(granitRole).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create GranitRole '{command.Name}': " +
                string.Join("; ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        try
        {
            var metadata = RoleMetadata.Create(
                roleId,
                command.Name,
                command.MultiTenancySides,
                command.TenantId,
                command.ClientId,
                command.Description,
                command.IsSystem);

            await roleMetadataStore.AddAsync(metadata, cancellationToken).ConfigureAwait(false);
            return metadata;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMetadataFailedCompensating(logger, ex, roleId, command.Name);
            await CompensateDeleteAsync(granitRole).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<RoleMetadata> ExecuteCompensatingRenameAsync(
        Guid roleId,
        string newName,
        string? newDescription,
        CancellationToken cancellationToken)
    {
        RoleMetadata? existingMetadata = await roleMetadataStore.FindByIdAsync(roleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No RoleMetadata found for id {roleId:D}.");

        if (existingMetadata.IsSystem)
        {
            throw new InvalidOperationException(
                $"Role '{existingMetadata.Name}' is a system role and cannot be renamed.");
        }

        GranitRole granitRole = await roleManager.FindByIdAsync(roleId.ToString("D"))
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No GranitRole found for id {roleId:D}.");

        string previousName = granitRole.Name!;
        string? previousDescription = granitRole.Description;

        granitRole.Name = newName;
        granitRole.Description = newDescription;
        IdentityResult updateResult = await roleManager.UpdateAsync(granitRole).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to update GranitRole '{previousName}': " +
                string.Join("; ", updateResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        try
        {
            existingMetadata.Rename(newName, newDescription);
            await roleMetadataStore.UpdateAsync(existingMetadata, cancellationToken).ConfigureAwait(false);
            return existingMetadata;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRenameFailedCompensating(logger, ex, roleId, newName);
            granitRole.Name = previousName;
            granitRole.Description = previousDescription;
            try
            {
                await roleManager.UpdateAsync(granitRole).ConfigureAwait(false);
            }
            catch (Exception compEx) when (compEx is not OperationCanceledException)
            {
                LogCompensationFailed(logger, compEx, roleId);
            }

            throw;
        }
    }

    private async Task ExecuteCompensatingDeleteAsync(Guid roleId, CancellationToken cancellationToken)
    {
        RoleMetadata? metadata = await roleMetadataStore.FindByIdAsync(roleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No RoleMetadata found for id {roleId:D}.");

        if (metadata.IsSystem)
        {
            throw new InvalidOperationException(
                $"Role '{metadata.Name}' is a system role and cannot be deleted.");
        }

        GranitRole? granitRole = await roleManager.FindByIdAsync(roleId.ToString("D")).ConfigureAwait(false);

        await roleMetadataStore.RemoveAsync(metadata, cancellationToken).ConfigureAwait(false);

        if (granitRole is not null)
        {
            IdentityResult deleteResult = await roleManager.DeleteAsync(granitRole).ConfigureAwait(false);
            if (!deleteResult.Succeeded)
            {
                // Metadata is already gone — surface the error so the operator knows
                // a GranitRole orphan is now in the Identity store. The FK-free nature
                // of the dual model means no referential integrity breakage, but the
                // role should be cleaned up.
                throw new InvalidOperationException(
                    $"RoleMetadata for '{metadata.Name}' deleted but GranitRole cleanup failed: " +
                    string.Join("; ", deleteResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
            }
        }
    }

    private async Task CompensateDeleteAsync(GranitRole granitRole)
    {
        try
        {
            IdentityResult deleteResult = await roleManager.DeleteAsync(granitRole).ConfigureAwait(false);
            if (!deleteResult.Succeeded)
            {
                LogCompensationFailed(logger, exception: null, granitRole.Id);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCompensationFailed(logger, ex, granitRole.Id);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Logging
    // ────────────────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "GranitRoleOrchestrator: atomic shared-connection transaction path selected.")]
    private static partial void LogAtomicPathSelected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "GranitRoleOrchestrator: atomic path unavailable ({Reason}); falling back to compensating-write strategy.")]
    private static partial void LogAtomicPathUnavailable(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "GranitRoleOrchestrator: atomic transaction rolled back for role {RoleId} ('{RoleName}'); neither Identity nor metadata rows persisted.")]
    private static partial void LogAtomicTransactionRolledBack(
        ILogger logger, Guid roleId, string? roleName);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "RoleMetadata creation failed for role {RoleId} ('{RoleName}') — compensating by deleting GranitRole.")]
    private static partial void LogMetadataFailedCompensating(
        ILogger logger, Exception exception, Guid roleId, string roleName);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "RoleMetadata rename failed for role {RoleId} → '{NewName}' — compensating by reverting GranitRole.")]
    private static partial void LogRenameFailedCompensating(
        ILogger logger, Exception exception, Guid roleId, string newName);

    [LoggerMessage(Level = LogLevel.Critical,
        Message = "Compensation action failed for role {RoleId} — manual cleanup required (orphan GranitRole or RoleMetadata row).")]
    private static partial void LogCompensationFailed(
        ILogger logger, Exception? exception, Guid roleId);

    // ────────────────────────────────────────────────────────────────────
    // Types
    // ────────────────────────────────────────────────────────────────────

    private readonly record struct AtomicResolution(bool IsAtomic, DbContext? IdentityContext, DbContext? HostContext)
    {
        public static readonly AtomicResolution NotAtomic = new(false, null, null);
    }
}
