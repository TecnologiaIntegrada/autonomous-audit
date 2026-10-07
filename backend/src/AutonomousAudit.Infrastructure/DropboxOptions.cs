namespace AutonomousAudit.Infrastructure;

public sealed class DropboxOptions
{
    public const string SectionName = "Dropbox";

    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 180;
    public string RootPath { get; set; } = "/AutonomousAudit";
}
