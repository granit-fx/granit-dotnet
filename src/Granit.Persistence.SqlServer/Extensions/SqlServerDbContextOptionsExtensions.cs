using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Granit.Persistence.SqlServer.Extensions;

/// <summary>
/// Extension methods for configuring <see cref="DbContextOptionsBuilder"/> with
/// opinionated SQL Server defaults.
/// </summary>
public static class SqlServerDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the <see cref="DbContextOptionsBuilder"/> to use SQL Server with
    /// Granit's opinionated defaults:
    /// <list type="bullet">
    ///   <item><see cref="SqlServerDbContextOptionsBuilder.EnableRetryOnFailure(int, TimeSpan, IEnumerable{int}?)"/>
    ///         — max 3 retries, 30-second max delay.</item>
    ///   <item>Command timeout of 30 seconds.</item>
    /// </list>
    /// Pass <paramref name="sqlServerOptionsAction"/> to override or extend individual settings.
    /// </summary>
    /// <param name="optionsBuilder">The options builder to configure.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="sqlServerOptionsAction">Optional additional SQL Server configuration.</param>
    /// <returns>The options builder for chaining.</returns>
    public static DbContextOptionsBuilder UseGranitSqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null) =>
        optionsBuilder.UseSqlServer(connectionString, b =>
        {
            b.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            b.CommandTimeout(30);
            sqlServerOptionsAction?.Invoke(b);
        });

    /// <inheritdoc cref="UseGranitSqlServer(DbContextOptionsBuilder, string, Action{SqlServerDbContextOptionsBuilder}?)"/>
    public static DbContextOptionsBuilder<TContext> UseGranitSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)UseGranitSqlServer(
            (DbContextOptionsBuilder)optionsBuilder, connectionString, sqlServerOptionsAction);
}
