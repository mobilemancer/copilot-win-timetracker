using System.Text.RegularExpressions;
using TimeTracker.Domain;

namespace TimeTracker.Application;

public sealed class CalendarInferenceService
{
    public TimeEntryDraft CreateDraft(CalendarMeeting meeting, TimeTrackerState state)
    {
        var customerMatches = GetCustomerMatches(meeting, state).ToList();
        var projectMatches = GetProjectMatches(meeting, state).ToList();

        var selectedCustomer = customerMatches.Count == 1
            ? customerMatches[0].Name
            : null;

        ProjectDefinition? selectedProject = null;
        if (!string.IsNullOrWhiteSpace(selectedCustomer))
        {
            var projectsForCustomer = projectMatches
                .Where(project => string.Equals(project.CustomerName, selectedCustomer, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(project => $"{project.CustomerName}\u001F{project.Name}", StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (projectsForCustomer.Count == 1)
            {
                selectedProject = projectsForCustomer[0];
            }
        }
        else if (projectMatches.Count == 1)
        {
            selectedProject = projectMatches[0];
            selectedCustomer = selectedProject.CustomerName;
        }

        return new TimeEntryDraft
        {
            PromptTitle = "How should this meeting be booked?",
            StartTime = meeting.StartTime,
            EndTime = meeting.EndTime,
            SelectedCustomerName = selectedCustomer,
            SelectedProjectName = selectedProject?.Name,
            Note = meeting.Subject,
            Source = selectedCustomer is not null && selectedProject is not null
                ? TimeEntrySource.Calendar
                : TimeEntrySource.MeetingFollowUp,
            CalendarAccountEmail = meeting.AccountDisplayName,
            CalendarEventId = meeting.EventId,
        };
    }

    public bool IsResolved(TimeEntryDraft draft)
    {
        return !string.IsNullOrWhiteSpace(draft.SelectedCustomerName)
            && !string.IsNullOrWhiteSpace(draft.SelectedProjectName);
    }

    private static IEnumerable<Customer> GetCustomerMatches(CalendarMeeting meeting, TimeTrackerState state)
    {
        var matchText = BuildMatchText(meeting);
        foreach (var customer in state.Customers)
        {
            if (string.IsNullOrWhiteSpace(customer.Name))
            {
                continue;
            }

            var normalizedName = Normalize(customer.Name);
            if (matchText.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                yield return customer;
                continue;
            }

            if (EmailDomains(meeting).Any(domain => domain.Contains(normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                yield return customer;
            }
        }
    }

    private static IEnumerable<ProjectDefinition> GetProjectMatches(CalendarMeeting meeting, TimeTrackerState state)
    {
        var matchText = BuildMatchText(meeting);
        foreach (var project in state.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.Name))
            {
                continue;
            }

            var normalizedName = Normalize(project.Name);
            if (matchText.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                yield return project;
            }
        }
    }

    private static IEnumerable<string> EmailDomains(CalendarMeeting meeting)
    {
        var emails = meeting.AttendeeEmails
            .Concat([meeting.OrganizerEmail ?? string.Empty])
            .Where(email => !string.IsNullOrWhiteSpace(email));

        foreach (var email in emails)
        {
            var at = email.IndexOf('@');
            if (at > -1 && at + 1 < email.Length)
            {
                yield return email[(at + 1)..];
            }
        }
    }

    private static string BuildMatchText(CalendarMeeting meeting)
    {
        return string.Join(
            ' ',
            new[]
            {
                meeting.Subject,
                meeting.BodyPreview ?? string.Empty,
                meeting.OrganizerEmail ?? string.Empty,
                string.Join(' ', meeting.AttendeeEmails),
            }.Select(Normalize));
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }
}
