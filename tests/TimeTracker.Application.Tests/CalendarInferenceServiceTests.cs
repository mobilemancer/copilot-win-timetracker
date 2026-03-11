using TimeTracker.Application;
using TimeTracker.Domain;

namespace TimeTracker.Application.Tests;

public sealed class CalendarInferenceServiceTests
{
    [Fact]
    public void CreateDraft_InfersCustomerAndProjectFromMeetingText()
    {
        var service = new CalendarInferenceService();
        var state = new TimeTrackerState
        {
            Customers =
            [
                new Customer { Name = "Internal", IsInternal = true },
                new Customer { Name = "Contoso" },
            ],
            Projects =
            [
                new ProjectDefinition { CustomerName = "Contoso", Name = "Migration" },
            ],
        };

        var draft = service.CreateDraft(
            new CalendarMeeting
            {
                AccountDisplayName = "user@company.com",
                EventId = "evt-1",
                Subject = "Contoso migration planning",
                StartTime = new DateTimeOffset(2026, 3, 11, 9, 0, 0, TimeSpan.Zero),
                EndTime = new DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero),
                OrganizerEmail = "pm@contoso.com",
                AttendeeEmails = ["user@company.com"],
            },
            state);

        Assert.Equal("Contoso", draft.SelectedCustomerName);
        Assert.Equal("Migration", draft.SelectedProjectName);
        Assert.Equal(TimeEntrySource.Calendar, draft.Source);
    }

    [Fact]
    public void CreateDraft_LeavesProjectUnresolvedWhenOnlyCustomerMatches()
    {
        var service = new CalendarInferenceService();
        var state = new TimeTrackerState
        {
            Customers =
            [
                new Customer { Name = "Internal", IsInternal = true },
                new Customer { Name = "Contoso" },
            ],
            Projects =
            [
                new ProjectDefinition { CustomerName = "Contoso", Name = "Migration" },
                new ProjectDefinition { CustomerName = "Contoso", Name = "Support" },
            ],
        };

        var draft = service.CreateDraft(
            new CalendarMeeting
            {
                AccountDisplayName = "user@company.com",
                EventId = "evt-2",
                Subject = "Weekly Contoso sync",
                StartTime = new DateTimeOffset(2026, 3, 11, 9, 0, 0, TimeSpan.Zero),
                EndTime = new DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero),
                OrganizerEmail = "pm@contoso.com",
                AttendeeEmails = ["user@company.com"],
            },
            state);

        Assert.Equal("Contoso", draft.SelectedCustomerName);
        Assert.Null(draft.SelectedProjectName);
        Assert.Equal(TimeEntrySource.MeetingFollowUp, draft.Source);
    }
}
