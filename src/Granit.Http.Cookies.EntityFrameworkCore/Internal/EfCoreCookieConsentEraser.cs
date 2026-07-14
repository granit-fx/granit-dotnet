using Granit.Http.Cookies.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Granit.Http.Cookies.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ICookieConsentEraser"/> — GDPR Art. 17 hard delete
/// of the consent records attributable to a data subject.
/// </summary>
/// <remarks>
/// Matches on the <c>CreatedBy</c> audit field: it is the only subject link a consent
/// record carries (stamped when an authenticated user posted the decision). Anonymous
/// records (empty <c>CreatedBy</c>) contain no personal data — irreversibly masked IP,
/// truncated user-agent — and are retained as accountability evidence.
/// </remarks>
internal sealed class EfCoreCookieConsentEraser(
    IDbContextFactory<CookiesDbContext> contextFactory) : ICookieConsentEraser
{
    /// <inheritdoc/>
    public async Task<int> EraseUserDataAsync(
        string userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using CookiesDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.ConsentRecords
            .Where(r => r.CreatedBy == userId && r.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
