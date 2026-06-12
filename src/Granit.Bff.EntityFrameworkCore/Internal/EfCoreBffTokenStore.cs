using System.Text.Json;
using Granit.Bff.Options;
using Granit.Encryption;
using Granit.Guids;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Bff.EntityFrameworkCore.Internal;

/// <summary>
/// <see cref="IBffTokenStore"/> implementation backed by EF Core.
/// Alternative to <c>DistributedCacheBffTokenStore</c> for deployments without Redis.
/// </summary>
/// <remarks>
/// Tokens are encrypted at rest using <see cref="IStringEncryptionService"/> when available.
/// If no encryption service is configured, tokens are stored as plaintext JSON and a warning
/// is logged at startup (ISO 27001 A.8.24).
/// </remarks>
internal sealed partial class EfCoreBffTokenStore(
    IDbContextFactory<BffDbContext> dbContextFactory,
    IOptions<GranitBffOptions> options,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILogger<EfCoreBffTokenStore> logger,
    IStringEncryptionService? encryptionService = null) : IBffTokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly bool _encryptionEnabled = InitEncryption(encryptionService, logger);

    public async Task StoreAsync(string frontendName, string sessionId, BffTokenSet tokens, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(tokens);

        await using BffDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        BffSessionEntity? existing = await db.Sessions
            .FirstOrDefaultAsync(s => s.FrontendName == frontendName && s.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        string serialized = SerializeTokens(tokens);
        DateTimeOffset expiresAt = clock.Now.Add(options.Value.SessionDuration);
        DateTimeOffset lastAccessedAt = tokens.LastAccessedAt ?? clock.Now;
        string? storedIp = EncryptIp(tokens.IpAddress);

        if (existing is not null)
        {
            existing.SerializedTokens = serialized;
            existing.ExpiresAt = expiresAt;
            existing.UserId = tokens.UserId;
            existing.LastAccessedAt = lastAccessedAt;
            existing.IpAddress = storedIp;
        }
        else
        {
            db.Sessions.Add(new BffSessionEntity
            {
                Id = guidGenerator.Create(),
                SessionId = sessionId,
                FrontendName = frontendName,
                UserId = tokens.UserId,
                SerializedTokens = serialized,
                ExpiresAt = expiresAt,
                CreatedAt = clock.Now,
                LastAccessedAt = lastAccessedAt,
                IpAddress = storedIp,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<BffTokenSet?> GetAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await using BffDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        BffSessionEntity? entity = await db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.FrontendName == frontendName && s.SessionId == sessionId && s.ExpiresAt > clock.Now,
                cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        BffTokenSet? tokens = DeserializeTokens(entity.SerializedTokens);

        // The dedicated columns are authoritative for these fields (TouchAsync updates them without rewriting
        // the encrypted blob), so overlay them onto the deserialized token set.
        return tokens is null
            ? null
            : tokens with { LastAccessedAt = entity.LastAccessedAt, IpAddress = DecryptIp(entity.IpAddress) };
    }

    public async Task TouchAsync(
        string frontendName, string sessionId, DateTimeOffset lastAccessedAt, string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        string? storedIp = EncryptIp(ipAddress);

        await using BffDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Column-only update — avoids loading, decrypting, and re-encrypting the token blob on every touch.
        await db.Sessions
            .Where(s => s.FrontendName == frontendName && s.SessionId == sessionId && s.ExpiresAt > clock.Now)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.LastAccessedAt, lastAccessedAt)
                    .SetProperty(x => x.IpAddress, storedIp),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await using BffDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // ExecuteDeleteAsync intentional — BffSessionEntity is neither audited nor soft-deletable,
        // so bypassing interceptors is safe and avoids loading the entity into memory.
        await db.Sessions
            .Where(s => s.FrontendName == frontendName && s.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetSessionIdsByUserAsync(
        string frontendName, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using BffDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.Sessions
            .AsNoTracking()
            .Where(s => s.FrontendName == frontendName && s.UserId == userId && s.ExpiresAt > clock.Now)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.SessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private string SerializeTokens(BffTokenSet tokens)
    {
        string json = JsonSerializer.Serialize(tokens, JsonOptions);
        return _encryptionEnabled ? encryptionService!.Encrypt(json) : json;
    }

    private BffTokenSet? DeserializeTokens(string data)
    {
        string json = _encryptionEnabled ? encryptionService!.Decrypt(data) ?? data : data;
        return JsonSerializer.Deserialize<BffTokenSet>(json, JsonOptions);
    }

    private string? EncryptIp(string? ipAddress) =>
        ipAddress is null ? null : _encryptionEnabled ? encryptionService!.Encrypt(ipAddress) : ipAddress;

    private string? DecryptIp(string? stored) =>
        stored is null ? null : _encryptionEnabled ? encryptionService!.Decrypt(stored) ?? stored : stored;

    private static bool InitEncryption(IStringEncryptionService? service, ILogger logger)
    {
        if (service is not null)
        {
            return true;
        }

        LogEncryptionNotConfigured(logger);
        return false;
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BFF EF Core token store: no IStringEncryptionService registered — "
            + "tokens are stored as plaintext JSON (ISO 27001 A.8.24 non-conformant). "
            + "Register Granit.Encryption to enable at-rest encryption")]
    private static partial void LogEncryptionNotConfigured(ILogger logger);
}
