using Granit.Contacts.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Contacts.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering EF Core persistence for Granit.Contacts.</summary>
public static class ContactsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers the <c>ContactsDbContext</c> + <c>EfContactStore</c> + queryable source.</summary>
    public static IHostApplicationBuilder AddGranitContactsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<ContactsDbContext>(configure);

        builder.Services.AddScoped<EfContactStore>();
        builder.Services.TryAddScoped<IContactReader>(sp => sp.GetRequiredService<EfContactStore>());
        builder.Services.TryAddScoped<IContactWriter>(sp => sp.GetRequiredService<EfContactStore>());
        builder.Services.TryAddScoped<IDefaultContactResolver, EfDefaultContactResolver>();
        builder.Services.TryAddScoped<IDefaultContactSeeder, EfDefaultContactSeeder>();

        builder.Services.AddScoped<IQueryableSource<Domain.Contact>, EfContactQueryableSource>();

        return builder;
    }
}
