namespace TimeTracker.Domain;

public sealed class Office365AccountSettings
{
    public string DisplayName { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
