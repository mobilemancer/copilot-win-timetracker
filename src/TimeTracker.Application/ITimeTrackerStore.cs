namespace TimeTracker.Application;

public interface ITimeTrackerStore
{
    TimeTrackerState Load();

    void Save(TimeTrackerState state);
}
