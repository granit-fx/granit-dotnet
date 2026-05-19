using System.Diagnostics.CodeAnalysis;

namespace Granit.DataExchange.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.DataExchange.Endpoints</c> module.
/// Use these names when granting permissions via <c>IPermissionManagerWriter.SetAsync()</c>
/// or when checking access via <c>IPermissionChecker.IsGrantedAsync()</c>.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Permission resource names follow [Module].[Resource].[Action] convention")]
public static class DataExchangePermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "DataExchange";

    /// <summary>Permissions for the data import resource.</summary>
    public static class Imports
    {
        /// <summary>Grants read-only access to view import job history and status.</summary>
        public const string Read = "DataExchange.Imports.Read";

        /// <summary>
        /// Grants access to execute data imports
        /// (upload, preview, mappings, execute, dry-run, status, report, correction file).
        /// </summary>
        public const string Execute = "DataExchange.Imports.Execute";
    }

    /// <summary>Permissions for the data export resource.</summary>
    public static class Exports
    {
        /// <summary>Grants read-only access to view export definitions and job history.</summary>
        public const string Read = "DataExchange.Exports.Read";

        /// <summary>
        /// Grants access to execute data exports
        /// (definitions, field listing, export execution, download, presets).
        /// </summary>
        public const string Execute = "DataExchange.Exports.Execute";
    }
}
