namespace U360Prospect.Configuration;

public sealed class ActiveDirectorySettings
{
    public string Domain { get; set; } = string.Empty;

    public string LdapServer { get; set; } = string.Empty;

    public int LdapPort { get; set; } = 636;

    public bool UseLdaps { get; set; } = true;
}
