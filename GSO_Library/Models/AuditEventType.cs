namespace GSO_Library.Models;

public static class AuditEventType
{
    public const string LoginSuccess = "LoginSuccess";
    public const string LoginFailure = "LoginFailure";
    public const string TokenRefresh = "TokenRefresh";
    public const string FileDownload = "FileDownload";
    public const string AccountDisable = "AccountDisable";
    public const string AccountEnable = "AccountEnable";
    public const string RoleGrant = "RoleGrant";
    public const string RoleRemove = "RoleRemove";
}
