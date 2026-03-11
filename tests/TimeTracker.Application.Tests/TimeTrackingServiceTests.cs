using TimeTracker.Application;
using TimeTracker.Domain;

namespace TimeTracker.Application.Tests;

public sealed class TimeTrackingServiceTests
{
    [Fact]
    public void CreateDraft_UsesPreviousEntryEndAsStart()
    {
        var store = new InMemoryStore
        {
            State = new TimeTrackerState
            {
                Customers = [new Customer { Name = "Internal", IsInternal = true }],
                Projects = [new ProjectDefinition { CustomerName = "Internal", Name = "Admin" }],
                Entries =
                [
                    new TimeEntry
                    {
                        StartTime = new DateTimeOffset(2026, 3, 11, 8, 0, 0, TimeSpan.Zero),
                        EndTime = new DateTimeOffset(2026, 3, 11, 8, 30, 0, TimeSpan.Zero),
                        CustomerName = "Internal",
                        ProjectName = "Admin",
                    },
                ],
                Settings = new AppSettings(),
            },
        };

        var service = new TimeTrackingService(store);

        var draft = service.CreateDraft(new DateTimeOffset(2026, 3, 11, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 3, 11, 8, 30, 0, TimeSpan.Zero), draft.StartTime);
        Assert.Equal(new DateTimeOffset(2026, 3, 11, 9, 0, 0, TimeSpan.Zero), draft.EndTime);
    }

    [Fact]
    public void SaveMasterData_AlwaysPreservesInternalCustomer()
    {
        var store = new InMemoryStore();
        var service = new TimeTrackingService(store);

        service.SaveMasterData([], []);

        var state = service.LoadState();

        Assert.Contains(state.Customers, customer => customer.Name == "Internal");
    }

    private sealed class InMemoryStore : ITimeTrackerStore
    {
        public TimeTrackerState State { get; set; } = new();

        public TimeTrackerState Load()
        {
            return new TimeTrackerState
            {
                Customers = State.Customers.Select(customer => new Customer
                {
                    Name = customer.Name,
                    IsInternal = customer.IsInternal,
                }).ToList(),
                Projects = State.Projects.Select(project => new ProjectDefinition
                {
                    CustomerName = project.CustomerName,
                    Name = project.Name,
                }).ToList(),
                Entries = State.Entries.Select(entry => new TimeEntry
                {
                    Id = entry.Id,
                    StartTime = entry.StartTime,
                    EndTime = entry.EndTime,
                    CustomerName = entry.CustomerName,
                    ProjectName = entry.ProjectName,
                    Note = entry.Note,
                    Source = entry.Source,
                }).ToList(),
                Settings = new AppSettings
                {
                    WorkDayStart = State.Settings.WorkDayStart,
                    WorkDayEnd = State.Settings.WorkDayEnd,
                    IsPaused = State.Settings.IsPaused,
                    LaunchAtSignIn = State.Settings.LaunchAtSignIn,
                    ReminderIntervalMinutes = State.Settings.ReminderIntervalMinutes,
                    SummonHotkey = State.Settings.SummonHotkey,
                    Office365Accounts = State.Settings.Office365Accounts
                        .Select(account => new Office365AccountSettings
                        {
                            DisplayName = account.DisplayName,
                            TenantId = account.TenantId,
                            ClientId = account.ClientId,
                            Enabled = account.Enabled,
                        })
                        .ToList(),
                },
            };
        }

        public void Save(TimeTrackerState state)
        {
            State = state;
        }
    }
}
