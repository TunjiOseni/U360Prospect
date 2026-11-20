
namespace U360Prospect.Configuration;

public sealed class EntrustSettings
{
    public string ServiceUrl { get; set; } = string.Empty;

    public string UserGroup { get; set; } = string.Empty;

    public string RequesterId { get; set; } = string.Empty;

    public bool UseForwardedClientIp { get; set; } = false;

    public int TimeoutSeconds { get; set; } = 15;

    public bool AllowPocBypass { get; set; } = false;
}
