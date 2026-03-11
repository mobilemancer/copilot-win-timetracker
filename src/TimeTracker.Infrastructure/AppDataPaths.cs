namespace TimeTracker.Infrastructure;

public sealed class AppDataPaths
{
    public AppDataPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CopilotTimeTracker");
        CustomersFilePath = Path.Combine(RootDirectory, "customers.csv");
        ProjectsFilePath = Path.Combine(RootDirectory, "projects.csv");
        EntriesFilePath = Path.Combine(RootDirectory, "entries.csv");
        SettingsFilePath = Path.Combine(RootDirectory, "settings.json");
        TokenCacheDirectory = Path.Combine(RootDirectory, "tokens");
        CalendarPromptStateFilePath = Path.Combine(RootDirectory, "calendar-prompts.json");
    }

    public string RootDirectory { get; }

    public string CustomersFilePath { get; }

    public string ProjectsFilePath { get; }

    public string EntriesFilePath { get; }

    public string SettingsFilePath { get; }

    public string TokenCacheDirectory { get; }

    public string CalendarPromptStateFilePath { get; }
}
