using TimeTracker.Application;
using TimeTracker.Domain;

namespace TimeTracker.Application.Tests;

public sealed class ReminderSchedulerTests
{
    [Fact]
    public void GetDuePromptSlot_ReturnsSlotInsideWorkingHours()
    {
        var scheduler = new ReminderScheduler();

        var dueSlot = scheduler.GetDuePromptSlot(
            new DateTimeOffset(2026, 3, 11, 8, 30, 0, TimeSpan.Zero),
            new AppSettings(),
            lastPromptedSlot: null);

        Assert.Equal(new DateTimeOffset(2026, 3, 11, 8, 30, 0, TimeSpan.Zero), dueSlot);
    }

    [Fact]
    public void GetDuePromptSlot_ReturnsNullOutsideWorkingHours()
    {
        var scheduler = new ReminderScheduler();

        var dueSlot = scheduler.GetDuePromptSlot(
            new DateTimeOffset(2026, 3, 11, 18, 0, 0, TimeSpan.Zero),
            new AppSettings(),
            lastPromptedSlot: null);

        Assert.Null(dueSlot);
    }
}
