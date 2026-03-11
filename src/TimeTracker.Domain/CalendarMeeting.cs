namespace TimeTracker.Domain;

public sealed class CalendarMeeting
{
    public required string AccountDisplayName { get; init; }

    public required string EventId { get; init; }

    public required string Subject { get; init; }

    public string? BodyPreview { get; init; }

    public DateTimeOffset StartTime { get; init; }

    public DateTimeOffset EndTime { get; init; }

    public string? OrganizerEmail { get; init; }

    public IReadOnlyList<string> AttendeeEmails { get; init; } = [];
}
