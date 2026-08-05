using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace Granit.Testing.Persistence.Tests.Integration;

/// <summary>
/// Rolling-upgrade guarantee of the Postgres advisory-lock key transition (#3168): the new
/// lock version acquires BOTH the stable bigint key (SHA-256 prefix) and the legacy
/// <c>hashtext()</c> key, so a mixed fleet — old pods keyed on <c>hashtext</c> only, new
/// pods on both — can never end up with two concurrent "lock holders".
/// </summary>
[Collection(PostgresConformanceSuite.Name)]
public sealed class PostgresLockKeyTransitionTests(PostgresConformanceFixture fixture)
{
    /// <summary>Mirrors the (internal) production key derivation — locks the contract.</summary>
    private static long ComputeNewKey(string resource) =>
        BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(resource)));

    /// <summary>
    /// Unpooled connection: advisory locks are session-scoped, and a POOLED connection's
    /// session (with its locks) survives Close/Dispose — the simulated replicas here must
    /// actually release on close.
    /// </summary>
    private NpgsqlConnection CreateRawConnection() =>
        new($"{fixture.ConnectionString};Pooling=false");

    [Fact]
    public async Task Legacy_version_holder_blocks_the_new_lock_and_no_key_leaks()
    {
        string resource = $"transition-{Guid.CreateVersion7():N}";
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Simulate an OLD-version replica: it holds only the legacy hashtext() key.
        await using NpgsqlConnection legacyHolder = CreateRawConnection();
        await legacyHolder.OpenAsync(ct);
        (await ScalarAsync(legacyHolder, "SELECT pg_try_advisory_lock(hashtext($1))", resource, ct))
            .ShouldBe(true, "the simulated old replica must hold the legacy key");

        // The new lock wins the bigint key but must back off on the legacy key.
        IGranitMigrationLock newLock = fixture.CreateMigrationLock();
        IAsyncDisposable? handle = await newLock.TryAcquireAsync(resource, ct);
        handle.ShouldBeNull("a legacy-version holder must block the new lock (dual-acquire)");

        // The failed attempt must not leak the bigint key (session died with its connection).
        await using NpgsqlConnection probe = CreateRawConnection();
        await probe.OpenAsync(ct);
        (await ScalarAsync(probe, "SELECT pg_try_advisory_lock($1)", ComputeNewKey(resource), ct))
            .ShouldBe(true, "the loser of the dual-acquire must release the bigint key");
        await probe.CloseAsync();

        // Old replica releases (connection close releases its session lock) → new lock proceeds.
        await legacyHolder.CloseAsync();
        IAsyncDisposable? afterRelease = await newLock.TryAcquireAsync(resource, ct);
        afterRelease.ShouldNotBeNull("the new lock must acquire once the legacy holder is gone");
        await afterRelease.DisposeAsync();
    }

    [Fact]
    public async Task New_version_holder_blocks_a_legacy_version_acquirer()
    {
        string resource = $"transition-{Guid.CreateVersion7():N}";
        CancellationToken ct = TestContext.Current.CancellationToken;

        IGranitMigrationLock newLock = fixture.CreateMigrationLock();
        IAsyncDisposable? handle = await newLock.TryAcquireAsync(resource, ct);
        handle.ShouldNotBeNull();

        // An OLD-version replica contends on the legacy key only — it must be refused
        // because the new holder also took the hashtext() key.
        await using NpgsqlConnection legacyContender = CreateRawConnection();
        await legacyContender.OpenAsync(ct);
        (await ScalarAsync(legacyContender, "SELECT pg_try_advisory_lock(hashtext($1))", resource, ct))
            .ShouldBe(false, "an old-version replica must be blocked by the new holder");

        await handle.DisposeAsync();

        // After release, the legacy contender can acquire — both keys were released.
        (await ScalarAsync(legacyContender, "SELECT pg_try_advisory_lock(hashtext($1))", resource, ct))
            .ShouldBe(true, "release must free the legacy key too");
        await legacyContender.CloseAsync();
    }

    private static async Task<object?> ScalarAsync(
        NpgsqlConnection connection, string sql, object parameter, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(parameter);
        return await command.ExecuteScalarAsync(ct);
    }
}
