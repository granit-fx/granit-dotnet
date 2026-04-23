using System.Diagnostics;

namespace Granit.Identity.Federated.Cognito.Diagnostics;

/// <summary>OpenTelemetry activity source for the AWS Cognito identity provider.</summary>
internal static class IdentityCognitoActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Identity.Cognito";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string ListUsers = "cognito.list-users";
        public const string GetUser = "cognito.get-user";
        public const string CreateUser = "cognito.create-user";
        public const string UpdateUser = "cognito.update-user";
        public const string EnableUser = "cognito.enable-user";
        public const string DisableUser = "cognito.disable-user";
        public const string ListGroups = "cognito.list-groups";
        public const string ListGroupsForUser = "cognito.list-groups-for-user";
        public const string AddUserToGroup = "cognito.add-user-to-group";
        public const string RemoveUserFromGroup = "cognito.remove-user-from-group";
        public const string GlobalSignOut = "cognito.global-sign-out";
        public const string ListUserPoolClients = "cognito.list-user-pool-clients";
        public const string GetClientRoles = "cognito.get-client-roles";
        public const string GetUserClientRoles = "cognito.get-user-client-roles";
        public const string CreateClientRole = "cognito.create-client-role";
        public const string AssignClientRole = "cognito.assign-client-role";
        public const string RemoveClientRole = "cognito.remove-client-role";
#pragma warning disable GRSEC003 // Operation names, not secrets
        public const string SetPassword = "cognito.set-password";
        public const string ResetPassword = "cognito.reset-password";
        public const string VerifyCredentials = "cognito.verify-credentials";
#pragma warning restore GRSEC003
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string UserPoolId = "cognito.user_pool_id";
        public const string UserId = "cognito.user_id";
        public const string ClientId = "cognito.client_id";
    }
}
