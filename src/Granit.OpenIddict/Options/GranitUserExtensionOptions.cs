namespace Granit.OpenIddict.Options;

/// <summary>
/// Options for extending <c>GranitUser</c> with additional SQL columns at startup.
/// </summary>
/// <remarks>
/// <para>
/// Mapped properties are added as EF Core Shadow Properties on the <c>oidc_users</c> table.
/// They are indexable and searchable via SQL, unlike <c>CustomAttributesJson</c> (JSONB).
/// </para>
/// <para>
/// <b>Rule:</b> a property mapped as a SQL column is <b>excluded</b> from
/// <c>CustomAttributesJson</c> to avoid data duplication. The
/// <c>ExtraPropertySyncInterceptor</c> enforces this at save time.
/// </para>
/// <para>
/// After adding mappings, the host application must regenerate EF Core migrations:
/// <code>dotnet ef migrations add AddUserExtensions --context OpenIddictDbContext</code>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.Configure&lt;GranitUserExtensionOptions&gt;(options =>
/// {
///     options.MapProperty&lt;string&gt;("JobTitle", maxLength: 128);
///     options.MapProperty&lt;string&gt;("Department", maxLength: 64);
///     options.MapProperty&lt;bool&gt;("IsVip");
/// });
/// </code>
/// </example>
public sealed class GranitUserExtensionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OpenIddict:UserExtensions";

    /// <summary>
    /// Gets the list of user property mappings.
    /// </summary>
    public List<UserPropertyMapping> Mappings { get; } = [];

    /// <summary>
    /// Maps a property as a SQL column on the <c>oidc_users</c> table.
    /// </summary>
    /// <typeparam name="T">The CLR type of the property.</typeparam>
    /// <param name="name">The property name (used as column name and ExtraProperties key).</param>
    /// <param name="maxLength">Maximum string length (only for <see cref="string"/> properties).</param>
    /// <param name="isRequired">Whether the column is NOT NULL. Default: <see langword="false"/>.</param>
    public void MapProperty<T>(string name, int? maxLength = null, bool isRequired = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Mappings.Add(new UserPropertyMapping(name, typeof(T), maxLength, isRequired));
    }
}

/// <summary>
/// Describes a single user extension property mapping.
/// </summary>
/// <param name="Name">The property name (column name + ExtraProperties key).</param>
/// <param name="ClrType">The CLR type of the property.</param>
/// <param name="MaxLength">Maximum string length (null for non-string types).</param>
/// <param name="IsRequired">Whether the column is NOT NULL.</param>
public sealed record UserPropertyMapping(
    string Name,
    Type ClrType,
    int? MaxLength,
    bool IsRequired);
