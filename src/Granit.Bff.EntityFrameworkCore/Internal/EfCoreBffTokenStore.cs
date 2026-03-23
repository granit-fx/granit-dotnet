using System.Text.Json;
using Granit.Bff.Options;
using Granit.Guids;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.Bff.EntityFrameworkCore.Internal;

/// <summary>
/// <see cref="IBffTokenStore"/> implementation backed by EF Core.
/// Alternative to <c>DistributedCacheBffTokenStore</c> for deployments without Redis.
/// </summary>
internal sealed class EfCoreBffTokenStore(
    IDbContextFactory<BffDbContext> dbContextFactory,
    IOptions<GranitBffOptions> options,
    IGuidGenerator guidGenerator,
    IClock clock) : IBffTokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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

        string serialized = JsonSerializer.Serialize(tokens, JsonOptions);
        DateTimeOffset expiresAt = clock.Now.Add(options.Value.SessionDuration);

        if (existing is not null)
        {
            existing.SerializedTokens = serialized;
            existing.ExpiresAt = expiresAt;
            existing.UserId = tokens.UserId;
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

        return JsonSerializer.Deserialize<BffTokenSet>(entity.SerializedTokens, JsonOptions);
    }

    public async Task RemoveAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await using BffDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

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
            .Select(s => s.SessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
