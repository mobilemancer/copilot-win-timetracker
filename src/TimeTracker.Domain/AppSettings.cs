namespace TimeTracker.Domain;

public sealed class AppSettings
{
    public TimeSpan WorkDayStart { get; set; } = new(8, 0, 0);

    public TimeSpan WorkDayEnd { get; set; } = new(17, 0, 0);

    public bool IsPaused { get; set; }

    public bool LaunchAtSignIn { get; set; } = true;

    public string SummonHotkey { get; set; } = "Win+Ctrl+Alt+T";

    public int ReminderIntervalMinutes { get; set; } = 30;

    public List<Office365AccountSettings> Office365Accounts { get; set; } = [];
}
