namespace TimeTracker.Application;

public sealed class TimeEntryDraft
{
    public string PromptTitle { get; set; } = "What are you doing now?";

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }

    public string? SelectedCustomerName { get; set; }

    public string? SelectedProjectName { get; set; }

    public string? Note { get; set; }

    public string? ManualCustomerName { get; set; }

    public string? ManualProjectName { get; set; }

    public TimeTracker.Domain.TimeEntrySource Source { get; set; } = TimeTracker.Domain.TimeEntrySource.Manual;

    public string? CalendarAccountEmail { get; set; }

    public string? CalendarEventId { get; set; }
}
