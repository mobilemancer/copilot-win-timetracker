using System.Runtime.Versioning;
using TimeTracker.Application;
using TimeTracker.Domain;

namespace TimeTracker.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class Office365CalendarSyncService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeTrackingService _timeTrackingService;
    private readonly CalendarInferenceService _calendarInferenceService;
    private readonly Office365DeviceCodeAuthService _authService;
    private readonly GraphCalendarClient _graphCalendarClient;
    private readonly CalendarPromptStateStore _promptStateStore;

    public Office365CalendarSyncService(
        TimeTrackingService timeTrackingService,
        CalendarInferenceService calendarInferenceService,
        Office365DeviceCodeAuthService authService,
        GraphCalendarClient graphCalendarClient,
        CalendarPromptStateStore promptStateStore)
    {
        _timeTrackingService = timeTrackingService;
        _calendarInferenceService = calendarInferenceService;
        _authService = authService;
        _graphCalendarClient = graphCalendarClient;
        _promptStateStore = promptStateStore;
    }

    public async Task<IReadOnlyList<TimeEntryDraft>> SyncCompletedMeetingsAsync(
        Action<string>? deviceCodePrompt,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var state = _timeTrackingService.LoadState();
            var promptedKeys = _promptStateStore.Load();
            var trackedKeys = state.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.CalendarAccountEmail) && !string.IsNullOrWhiteSpace(entry.CalendarEventId))
                .Select(entry => GetEventKey(entry.CalendarAccountEmail!, entry.CalendarEventId!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var followUpDrafts = new List<TimeEntryDraft>();
            var now = DateTimeOffset.Now;
            var start = now.AddDays(-2);
            var end = now.AddHours(1);

            foreach (var account in state.Settings.Office365Accounts.Where(IsUsable))
            {
                var accessToken = await _authService.GetAccessTokenAsync(account, deviceCodePrompt, cancellationToken);
                var meetings = await _graphCalendarClient.GetCompletedMeetingsAsync(account.DisplayName, accessToken, start, end, now, cancellationToken);

                foreach (var meeting in meetings.OrderBy(meeting => meeting.EndTime))
                {
                    var key = GetEventKey(meeting.AccountDisplayName, meeting.EventId);
                    if (trackedKeys.Contains(key))
                    {
                        continue;
                    }

                    var draft = _calendarInferenceService.CreateDraft(meeting, state);
                    if (_calendarInferenceService.IsResolved(draft))
                    {
                        _timeTrackingService.SaveEntry(new TimeEntry
                        {
                            StartTime = draft.StartTime,
                            EndTime = draft.EndTime,
                            CustomerName = draft.SelectedCustomerName!,
                            ProjectName = draft.SelectedProjectName!,
                            Note = draft.Note,
                            Source = TimeEntrySource.Calendar,
                            CalendarAccountEmail = draft.CalendarAccountEmail,
                            CalendarEventId = draft.CalendarEventId,
                        });
                        trackedKeys.Add(key);
                        continue;
                    }

                    if (promptedKeys.Add(key))
                    {
                        followUpDrafts.Add(draft);
                    }
                }
            }

            _promptStateStore.Save(promptedKeys);
            return followUpDrafts;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsUsable(Office365AccountSettings account)
    {
        return account.Enabled
            && !string.IsNullOrWhiteSpace(account.DisplayName)
            && !string.IsNullOrWhiteSpace(account.TenantId)
            && !string.IsNullOrWhiteSpace(account.ClientId);
    }

    private static string GetEventKey(string accountDisplayName, string eventId)
    {
        return $"{accountDisplayName}\u001F{eventId}";
    }
}
