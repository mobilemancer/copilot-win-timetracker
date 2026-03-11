using TimeTracker.Domain;

namespace TimeTracker.Application;

public sealed class ReminderScheduler
{
    public DateTimeOffset? GetDuePromptSlot(
        DateTimeOffset now,
        AppSettings settings,
        DateTimeOffset? lastPromptedSlot)
    {
        if (settings.IsPaused || settings.ReminderIntervalMinutes <= 0)
        {
            return null;
        }

        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return null;
        }

        var slot = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            now.Minute,
            0,
            now.Offset);

        if (slot.Minute % settings.ReminderIntervalMinutes != 0)
        {
            return null;
        }

        var timeOfDay = slot.TimeOfDay;
        if (timeOfDay < settings.WorkDayStart || timeOfDay > settings.WorkDayEnd)
        {
            return null;
        }

        if (lastPromptedSlot.HasValue && lastPromptedSlot.Value == slot)
        {
            return null;
        }

        return slot;
    }
}
