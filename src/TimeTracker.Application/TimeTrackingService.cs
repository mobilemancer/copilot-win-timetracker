using TimeTracker.Domain;

namespace TimeTracker.Application;

public sealed class TimeTrackingService
{
    private readonly object _gate = new();
    private readonly ITimeTrackerStore _store;

    public TimeTrackingService(ITimeTrackerStore store)
    {
        _store = store;
    }

    public TimeTrackerState LoadState()
    {
        lock (_gate)
        {
            var state = _store.Load();
            var normalized = Normalize(state);
            _store.Save(normalized);
            return Clone(normalized);
        }
    }

    public TimeEntryDraft CreateDraft(DateTimeOffset promptTime)
    {
        var state = LoadState();
        var normalizedPromptTime = new DateTimeOffset(
            promptTime.Year,
            promptTime.Month,
            promptTime.Day,
            promptTime.Hour,
            promptTime.Minute,
            0,
            promptTime.Offset);

        var previousEntry = state.Entries
            .Where(entry => entry.EndTime <= normalizedPromptTime)
            .OrderByDescending(entry => entry.EndTime)
            .FirstOrDefault();

        var startTime = previousEntry?.EndTime
            ?? normalizedPromptTime.AddMinutes(-state.Settings.ReminderIntervalMinutes);

        if (startTime >= normalizedPromptTime)
        {
            startTime = normalizedPromptTime.AddMinutes(-state.Settings.ReminderIntervalMinutes);
        }

        return new TimeEntryDraft
        {
            StartTime = startTime,
            EndTime = normalizedPromptTime,
            SelectedCustomerName = previousEntry?.CustomerName,
            SelectedProjectName = previousEntry?.ProjectName,
            Note = previousEntry?.Note,
        };
    }

    public void SaveEntry(TimeEntry entry)
    {
        ValidateEntry(entry);

        lock (_gate)
        {
            var state = Normalize(_store.Load());
            state.Entries.RemoveAll(existing => existing.Id == entry.Id);
            state.Entries.Add(Clone(entry));
            state = Normalize(state);
            _store.Save(state);
        }
    }

    public void SaveEntries(IEnumerable<TimeEntry> entries)
    {
        var clonedEntries = entries.Select(Clone).ToList();
        foreach (var entry in clonedEntries)
        {
            ValidateEntry(entry);
        }

        lock (_gate)
        {
            var state = Normalize(_store.Load());
            state.Entries = clonedEntries;
            state = Normalize(state);
            _store.Save(state);
        }
    }

    public void SaveMasterData(IEnumerable<Customer> customers, IEnumerable<ProjectDefinition> projects)
    {
        lock (_gate)
        {
            var state = Normalize(_store.Load());
            state.Customers = customers.Select(Clone).ToList();
            state.Projects = projects.Select(Clone).ToList();
            state = Normalize(state);
            _store.Save(state);
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            var state = Normalize(_store.Load());
            state.Settings = Clone(settings);
            state = Normalize(state);
            _store.Save(state);
        }
    }

    public IReadOnlyList<ProjectDefinition> GetProjectsForCustomer(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return [];
        }

        var state = LoadState();
        return state.Projects
            .Where(project => string.Equals(project.CustomerName, customerName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static TimeTrackerState Normalize(TimeTrackerState state)
    {
        state.Customers = state.Customers
            .Where(customer => !string.IsNullOrWhiteSpace(customer.Name))
            .GroupBy(customer => customer.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                first.Name = group.Key;
                return first;
            })
            .OrderBy(customer => customer.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!state.Customers.Any(customer => string.Equals(customer.Name, "Internal", StringComparison.OrdinalIgnoreCase)))
        {
            state.Customers.Insert(0, new Customer
            {
                Name = "Internal",
                IsInternal = true,
            });
        }

        state.Projects = state.Projects
            .Where(project => !string.IsNullOrWhiteSpace(project.CustomerName) && !string.IsNullOrWhiteSpace(project.Name))
            .GroupBy(project => $"{project.CustomerName.Trim()}\u001F{project.Name.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                var parts = group.Key.Split('\u001F');
                first.CustomerName = parts[0];
                first.Name = parts[1];
                return first;
            })
            .OrderBy(project => project.CustomerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        state.Entries = state.Entries
            .Where(entry => entry.EndTime > entry.StartTime)
            .OrderBy(entry => entry.StartTime)
            .ThenBy(entry => entry.EndTime)
            .ToList();

        state.Settings ??= new AppSettings();
        state.Settings.ReminderIntervalMinutes = state.Settings.ReminderIntervalMinutes <= 0
            ? 30
            : state.Settings.ReminderIntervalMinutes;
        state.Settings.SummonHotkey = string.IsNullOrWhiteSpace(state.Settings.SummonHotkey)
            ? "Win+Ctrl+Alt+T"
            : state.Settings.SummonHotkey.Trim();

        return state;
    }

    private static void ValidateEntry(TimeEntry entry)
    {
        if (entry.EndTime <= entry.StartTime)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }

        if (string.IsNullOrWhiteSpace(entry.CustomerName))
        {
            throw new InvalidOperationException("Customer is required.");
        }

        if (string.IsNullOrWhiteSpace(entry.ProjectName))
        {
            throw new InvalidOperationException("Project is required.");
        }
    }

    private static TimeTrackerState Clone(TimeTrackerState state)
    {
        return new TimeTrackerState
        {
            Customers = state.Customers.Select(Clone).ToList(),
            Projects = state.Projects.Select(Clone).ToList(),
            Entries = state.Entries.Select(Clone).ToList(),
            Settings = Clone(state.Settings),
        };
    }

    private static Customer Clone(Customer customer)
    {
        return new Customer
        {
            Name = customer.Name,
            IsInternal = customer.IsInternal,
        };
    }

    private static ProjectDefinition Clone(ProjectDefinition project)
    {
        return new ProjectDefinition
        {
            CustomerName = project.CustomerName,
            Name = project.Name,
        };
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            WorkDayStart = settings.WorkDayStart,
            WorkDayEnd = settings.WorkDayEnd,
            IsPaused = settings.IsPaused,
            LaunchAtSignIn = settings.LaunchAtSignIn,
            SummonHotkey = settings.SummonHotkey,
            ReminderIntervalMinutes = settings.ReminderIntervalMinutes,
            Office365Accounts = settings.Office365Accounts
                .Select(account => new Office365AccountSettings
                {
                    DisplayName = account.DisplayName,
                    TenantId = account.TenantId,
                    ClientId = account.ClientId,
                    Enabled = account.Enabled,
                })
                .ToList(),
        };
    }

    private static TimeEntry Clone(TimeEntry entry)
    {
        return new TimeEntry
        {
            Id = entry.Id,
            StartTime = entry.StartTime,
            EndTime = entry.EndTime,
            CustomerName = entry.CustomerName,
            ProjectName = entry.ProjectName,
            Note = entry.Note,
            ManualCustomerName = entry.ManualCustomerName,
            ManualProjectName = entry.ManualProjectName,
            Source = entry.Source,
            CalendarAccountEmail = entry.CalendarAccountEmail,
            CalendarEventId = entry.CalendarEventId,
        };
    }
}
