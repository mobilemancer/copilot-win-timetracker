using System.Text.Json;

namespace TimeTracker.Infrastructure;

public sealed class CalendarPromptStateStore
{
    private readonly AppDataPaths _paths;

    public CalendarPromptStateStore(AppDataPaths paths)
    {
        _paths = paths;
    }

    public HashSet<string> Load()
    {
        if (!File.Exists(_paths.CalendarPromptStateFilePath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(_paths.CalendarPromptStateFilePath))
            ?? [];
    }

    public void Save(HashSet<string> keys)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        File.WriteAllText(
            _paths.CalendarPromptStateFilePath,
            JsonSerializer.Serialize(keys.OrderBy(key => key).ToArray(), new JsonSerializerOptions { WriteIndented = true }));
    }
}
