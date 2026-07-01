using Granit.AI.Prompts.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IQueryableSource{TEntity}"/> for <see cref="PromptCategory"/>, backing the
/// category admin grid and export. When no tenant context is active (host admin) the multi-tenant
/// query filter is bypassed so categories are returned cross-tenant.
/// </summary>
internal sealed class EfPromptCategoryQueryableSource(
    IDbContextFactory<AIPromptsDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<PromptCategory>, IAsyncDisposable, IDisposable
{
    private AIPromptsDbContext? _context;

    public IQueryable<PromptCategory> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<PromptCategory> query = _context.PromptCategories.AsNoTracking();
        return scope.Restrict(query, typeof(PromptCategory).Name);
    }

    public ValueTask DisposeAsync()
    {
        AIPromptsDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
