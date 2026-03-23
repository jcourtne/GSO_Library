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
    public const string PasswordReset = "PasswordReset";
    public const string FileUpload = "FileUpload";
    public const string FileDelete = "FileDelete";
    public const string ArrangementCreate = "ArrangementCreate";
    public const string ArrangementDelete = "ArrangementDelete";
    public const string EnsembleCreate = "EnsembleCreate";
    public const string EnsembleDelete = "EnsembleDelete";
    public const string PerformanceCreate = "PerformanceCreate";
    public const string PerformanceDelete = "PerformanceDelete";
}
