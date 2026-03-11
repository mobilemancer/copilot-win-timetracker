using TimeTracker.Application;
using TimeTracker.Domain;
using TimeTracker.Infrastructure;

namespace TimeTracker.Application.Tests;

public sealed class CsvTimeTrackerStoreTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "copilot-timetracker-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsCustomersProjectsEntriesAndSettings()
    {
        var paths = CreatePaths();
        var store = new CsvTimeTrackerStore(paths);

        var state = new TimeTrackerState
        {
            Customers = [new Customer { Name = "Contoso" }],
            Projects = [new ProjectDefinition { CustomerName = "Contoso", Name = "Migration" }],
            Entries =
            [
                new TimeEntry
                {
                    Id = Guid.NewGuid(),
                    StartTime = new DateTimeOffset(2026, 3, 11, 8, 0, 0, TimeSpan.Zero),
                    EndTime = new DateTimeOffset(2026, 3, 11, 8, 30, 0, TimeSpan.Zero),
                    CustomerName = "Contoso",
                    ProjectName = "Migration",
                    Note = "Kickoff",
                    CalendarAccountEmail = "user@company.com",
                    CalendarEventId = "evt-1",
                    Source = TimeEntrySource.Calendar,
                },
            ],
            Settings = new AppSettings
            {
                SummonHotkey = "Ctrl+Alt+T",
                Office365Accounts =
                [
                    new Office365AccountSettings
                    {
                        DisplayName = "user@company.com",
                        TenantId = "tenant",
                        ClientId = "client",
                        Enabled = false,
                    },
                ],
            },
        };

        store.Save(state);
        var loaded = store.Load();

        Assert.Single(loaded.Customers);
        Assert.Single(loaded.Projects);
        Assert.Single(loaded.Entries);
        Assert.Single(loaded.Settings.Office365Accounts);
        Assert.Equal("Contoso", loaded.Customers[0].Name);
        Assert.Equal("Migration", loaded.Projects[0].Name);
        Assert.Equal("Kickoff", loaded.Entries[0].Note);
        Assert.Equal("evt-1", loaded.Entries[0].CalendarEventId);
        Assert.False(loaded.Settings.Office365Accounts[0].Enabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private AppDataPaths CreatePaths()
    {
        Directory.CreateDirectory(_rootDirectory);
        return new AppDataPaths(_rootDirectory);
    }
}
