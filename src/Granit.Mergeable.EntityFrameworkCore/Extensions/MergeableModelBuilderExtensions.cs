using Granit.Mergeable.EntityFrameworkCore.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Mergeable.EntityFrameworkCore.Extensions;

/// <summary>
/// ModelBuilder extension exposing the Mergeable-orchestrator entity configurations so host
/// applications can include the <c>granit.merge_idempotency</c> table in their own DbContext
/// (and migrations) when they prefer a single shared database over the isolated module
/// DbContext.
/// </summary>
public static class MergeableModelBuilderExtensions
{
    /// <summary>
    /// Registers the merge-orchestrator entities + indexes on the provided <paramref name="modelBuilder"/>.
    /// </summary>
    public static ModelBuilder ConfigureMergeableModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<MergeIdempotencyEntry>(b =>
        {
            b.ToTable("merge_idempotency", "granit");
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId);
            b.Property(e => e.Key).IsRequired().HasMaxLength(128);
            b.Property(e => e.RequestHash).IsRequired().HasMaxLength(64);
            b.Property(e => e.ResultJson).IsRequired();
            b.Property(e => e.ResultMac).IsRequired().HasMaxLength(64);
            b.Property(e => e.CreatedAt).IsRequired();

            // Lookup by (tenant, key, hash) powers replay; the UNIQUE constraint is the
            // second line of defence against double-merge when the HTTP idempotency
            // middleware misses (e.g. a buggy SDK that regenerates the key between two
            // retries of the same intent). Including TenantId in the index prevents the
            // cross-tenant key oracle (Tenant B cannot probe Tenant A's keys via 409 timing).
            b.HasIndex(e => new { e.TenantId, e.Key, e.RequestHash }).IsUnique();

            // Lookup by (tenant, survivor, loser) lets the orchestrator detect concurrent
            // attempts to merge the same pair under different idempotency keys.
            b.HasIndex(e => new { e.TenantId, e.SurvivorId, e.LoserId });

            // Garbage-collection helper.
            b.HasIndex(e => e.CreatedAt);
        });

        return modelBuilder;
    }
}
