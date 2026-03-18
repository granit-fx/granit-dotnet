using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Granit.Persistence.Postgres.Extensions;

/// <summary>
/// Extension methods for configuring <see cref="DbContextOptionsBuilder"/> with
/// opinionated Npgsql defaults.
/// </summary>
public static class NpgsqlDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the <see cref="DbContextOptionsBuilder"/> to use PostgreSQL via Npgsql
    /// with Granit's opinionated defaults:
    /// <list type="bullet">
    ///   <item><see cref="NpgsqlDbContextOptionsBuilder.EnableRetryOnFailure(int, TimeSpan, IEnumerable{string}?)"/>
    ///         — max 3 retries, 30-second max delay.</item>
    ///   <item>Command timeout of 30 seconds.</item>
    /// </list>
    /// Pass <paramref name="npgsqlOptionsAction"/> to override or extend individual settings.
    /// </summary>
    /// <param name="optionsBuilder">The options builder to configure.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="npgsqlOptionsAction">Optional additional Npgsql configuration.</param>
    /// <returns>The options builder for chaining.</returns>
    public static DbContextOptionsBuilder UseGranitNpgsql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null) =>
        optionsBuilder.UseNpgsql(connectionString, b =>
        {
            b.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
            b.CommandTimeout(30);
            npgsqlOptionsAction?.Invoke(b);
        });

    /// <inheritdoc cref="UseGranitNpgsql(DbContextOptionsBuilder, string, Action{NpgsqlDbContextOptionsBuilder}?)"/>
    public static DbContextOptionsBuilder<TContext> UseGranitNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)UseGranitNpgsql(
            (DbContextOptionsBuilder)optionsBuilder, connectionString, npgsqlOptionsAction);
}
