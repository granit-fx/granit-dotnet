using Granit.CustomerBalance.EntityFrameworkCore.Internal;
using Granit.Mergeable.Extensions;
using Granit.Parties.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.CustomerBalance.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering EF Core persistence for Granit.CustomerBalance.</summary>
public static class CustomerBalanceEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers EF Core persistence for Granit customer balance.</summary>
    public static IHostApplicationBuilder AddGranitCustomerBalanceEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<CustomerBalanceDbContext>(configure);

        builder.Services.AddScoped<EfBalanceAccountStore>();
        builder.Services.TryAddScoped<IBalanceAccountReader>(sp => sp.GetRequiredService<EfBalanceAccountStore>());
        builder.Services.TryAddScoped<IBalanceAccountWriter>(sp => sp.GetRequiredService<EfBalanceAccountStore>());

        builder.Services.AddScoped<EfBalanceTransactionReader>();
        builder.Services.TryAddScoped<IBalanceTransactionReader>(sp => sp.GetRequiredService<EfBalanceTransactionReader>());
        builder.Services.TryAddScoped<IBalanceTransactionWriter>(sp => sp.GetRequiredService<EfBalanceTransactionReader>());

        // Plugs CustomerBalance into the Party merge orchestrator. Unconditional registration:
        // when no IMergeService<Party> is wired up by the host, the rewriter just sits idle in
        // DI at zero runtime cost.
        builder.Services.AddReferenceRewriter<Party, BalanceAccountPartyReferenceRewriter>();

        return builder;
    }
}
