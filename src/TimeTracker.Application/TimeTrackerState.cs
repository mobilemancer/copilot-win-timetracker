using TimeTracker.Domain;

namespace TimeTracker.Application;

public sealed class TimeTrackerState
{
    public List<Customer> Customers { get; set; } = [];

    public List<ProjectDefinition> Projects { get; set; } = [];

    public List<TimeEntry> Entries { get; set; } = [];

    public AppSettings Settings { get; set; } = new();
}
