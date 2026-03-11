namespace TimeTracker.Domain;

public sealed class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? ManualCustomerName { get; set; }

    public string? ManualProjectName { get; set; }

    public TimeEntrySource Source { get; set; } = TimeEntrySource.Manual;

    public string? CalendarAccountEmail { get; set; }

    public string? CalendarEventId { get; set; }
}
