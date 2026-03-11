using System.Text;
using System.Text.Json;
using TimeTracker.Application;
using TimeTracker.Domain;

namespace TimeTracker.Infrastructure;

public sealed class CsvTimeTrackerStore : ITimeTrackerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly AppDataPaths _paths;

    public CsvTimeTrackerStore(AppDataPaths paths)
    {
        _paths = paths;
    }

    public TimeTrackerState Load()
    {
        Directory.CreateDirectory(_paths.RootDirectory);

        return new TimeTrackerState
        {
            Customers = LoadCustomers(),
            Projects = LoadProjects(),
            Entries = LoadEntries(),
            Settings = LoadSettings(),
        };
    }

    public void Save(TimeTrackerState state)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        SaveCustomers(state.Customers);
        SaveProjects(state.Projects);
        SaveEntries(state.Entries);
        SaveSettings(state.Settings);
    }

    private List<Customer> LoadCustomers()
    {
        if (!File.Exists(_paths.CustomersFilePath))
        {
            return [];
        }

        return File.ReadAllLines(_paths.CustomersFilePath, Encoding.UTF8)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(CsvHelpers.ParseRow)
            .Select(parts => new Customer
            {
                Name = parts.ElementAtOrDefault(0) ?? string.Empty,
                IsInternal = bool.TryParse(parts.ElementAtOrDefault(1), out var isInternal) && isInternal,
            })
            .ToList();
    }

    private List<ProjectDefinition> LoadProjects()
    {
        if (!File.Exists(_paths.ProjectsFilePath))
        {
            return [];
        }

        return File.ReadAllLines(_paths.ProjectsFilePath, Encoding.UTF8)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(CsvHelpers.ParseRow)
            .Select(parts => new ProjectDefinition
            {
                CustomerName = parts.ElementAtOrDefault(0) ?? string.Empty,
                Name = parts.ElementAtOrDefault(1) ?? string.Empty,
            })
            .ToList();
    }

    private List<TimeEntry> LoadEntries()
    {
        if (!File.Exists(_paths.EntriesFilePath))
        {
            return [];
        }

        return File.ReadAllLines(_paths.EntriesFilePath, Encoding.UTF8)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(CsvHelpers.ParseRow)
            .Select(parts => new TimeEntry
            {
                Id = Guid.TryParse(parts.ElementAtOrDefault(0), out var id) ? id : Guid.NewGuid(),
                StartTime = DateTimeOffset.Parse(parts.ElementAtOrDefault(1) ?? string.Empty),
                EndTime = DateTimeOffset.Parse(parts.ElementAtOrDefault(2) ?? string.Empty),
                CustomerName = parts.ElementAtOrDefault(3) ?? string.Empty,
                ProjectName = parts.ElementAtOrDefault(4) ?? string.Empty,
                Note = NullIfEmpty(parts.ElementAtOrDefault(5)),
                ManualCustomerName = NullIfEmpty(parts.ElementAtOrDefault(6)),
                ManualProjectName = NullIfEmpty(parts.ElementAtOrDefault(7)),
                Source = Enum.TryParse<TimeEntrySource>(parts.ElementAtOrDefault(8), out var source) ? source : TimeEntrySource.Manual,
                CalendarAccountEmail = NullIfEmpty(parts.ElementAtOrDefault(9)),
                CalendarEventId = NullIfEmpty(parts.ElementAtOrDefault(10)),
            })
            .ToList();
    }

    private AppSettings LoadSettings()
    {
        if (!File.Exists(_paths.SettingsFilePath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_paths.SettingsFilePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    private void SaveCustomers(IEnumerable<Customer> customers)
    {
        var lines = new List<string>
        {
            "Name,IsInternal",
        };

        lines.AddRange(customers.Select(customer => CsvHelpers.SerializeRow(
            [customer.Name, customer.IsInternal.ToString()])));
        WriteAllLinesAtomic(_paths.CustomersFilePath, lines);
    }

    private void SaveProjects(IEnumerable<ProjectDefinition> projects)
    {
        var lines = new List<string>
        {
            "CustomerName,Name",
        };

        lines.AddRange(projects.Select(project => CsvHelpers.SerializeRow(
            [project.CustomerName, project.Name])));
        WriteAllLinesAtomic(_paths.ProjectsFilePath, lines);
    }

    private void SaveEntries(IEnumerable<TimeEntry> entries)
    {
        var lines = new List<string>
        {
            "Id,StartTime,EndTime,CustomerName,ProjectName,Note,ManualCustomerName,ManualProjectName,Source,CalendarAccountEmail,CalendarEventId",
        };

        lines.AddRange(entries.Select(entry => CsvHelpers.SerializeRow(
        [
            entry.Id.ToString(),
            entry.StartTime.ToString("O"),
            entry.EndTime.ToString("O"),
            entry.CustomerName,
            entry.ProjectName,
            entry.Note,
            entry.ManualCustomerName,
            entry.ManualProjectName,
            entry.Source.ToString(),
            entry.CalendarAccountEmail,
            entry.CalendarEventId,
        ])));
        WriteAllLinesAtomic(_paths.EntriesFilePath, lines);
    }

    private void SaveSettings(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        WriteAllTextAtomic(_paths.SettingsFilePath, json);
    }

    private static void WriteAllLinesAtomic(string path, IEnumerable<string> lines)
    {
        WriteAllTextAtomic(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static void WriteAllTextAtomic(string path, string content)
    {
        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, content, Encoding.UTF8);
        File.Move(tempPath, path, true);
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
