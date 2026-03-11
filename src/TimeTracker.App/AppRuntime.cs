using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using TimeTracker.Application;
using TimeTracker.Infrastructure;
using WinForms = System.Windows.Forms;

namespace TimeTracker.App;

public sealed class AppRuntime : IDisposable
{
    private readonly ReminderScheduler _scheduler;
    private readonly TimeTrackingService _timeTrackingService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly Office365CalendarSyncService _calendarSyncService;
    private readonly DispatcherTimer _timer;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly GlobalHotkeyManager _hotkeyManager;

    private DateTimeOffset? _lastPromptSlot;
    private DateTimeOffset? _lastCalendarSyncSlot;
    private MainWindow? _dashboardWindow;
    private bool _syncInProgress;
    private bool _disposed;

    private AppRuntime(
        TimeTrackingService timeTrackingService,
        ReminderScheduler scheduler,
        StartupRegistrationService startupRegistrationService,
        Office365CalendarSyncService calendarSyncService)
    {
        _timeTrackingService = timeTrackingService;
        _scheduler = scheduler;
        _startupRegistrationService = startupRegistrationService;
        _calendarSyncService = calendarSyncService;

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Copilot Time Tracker",
            Visible = false,
        };

        _hotkeyManager = new GlobalHotkeyManager(OnSummonHotkeyPressed);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(1),
        };
        _timer.Tick += HandleTimerTick;
    }

    public static AppRuntime Create()
    {
        var paths = new AppDataPaths();
        var store = new CsvTimeTrackerStore(paths);
        var timeTrackingService = new TimeTrackingService(store);
        return new AppRuntime(
            timeTrackingService,
            new ReminderScheduler(),
            new StartupRegistrationService(),
            new Office365CalendarSyncService(
                timeTrackingService,
                new CalendarInferenceService(),
                new Office365DeviceCodeAuthService(new Office365TokenStore(paths)),
                new GraphCalendarClient(),
                new CalendarPromptStateStore(paths)));
    }

    public void Start()
    {
        var state = _timeTrackingService.LoadState();
        ApplySettings(state.Settings);
        ConfigureTrayMenu();
        _notifyIcon.Visible = true;
        _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _hotkeyManager.Dispose();
    }

    private void ConfigureTrayMenu()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Log time now", null, (_, _) => ShowQuickEntry(DateTimeOffset.Now));
        menu.Items.Add("Sync calendars now", null, async (_, _) => await SyncCalendarsAsync(showCompletionMessage: true));
        menu.Items.Add("Timeline / Overview", null, (_, _) => ShowDashboard(DashboardTab.Overview));
        menu.Items.Add("Settings", null, (_, _) => ShowDashboard(DashboardTab.Settings));
        menu.Items.Add("Pause / Resume", null, (_, _) => TogglePause());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.MouseClick += HandleTrayClick;
    }

    private void HandleTrayClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Left)
        {
            ShowQuickEntry(DateTimeOffset.Now);
        }
    }

    private async void HandleTimerTick(object? sender, EventArgs e)
    {
        var settings = _timeTrackingService.LoadState().Settings;
        var dueSlot = _scheduler.GetDuePromptSlot(DateTimeOffset.Now, settings, _lastPromptSlot);
        if (dueSlot.HasValue)
        {
            _lastPromptSlot = dueSlot.Value;
            ShowQuickEntry(dueSlot.Value);
        }

        var currentCalendarSlot = new DateTimeOffset(
            DateTimeOffset.Now.Year,
            DateTimeOffset.Now.Month,
            DateTimeOffset.Now.Day,
            DateTimeOffset.Now.Hour,
            (DateTimeOffset.Now.Minute / 5) * 5,
            0,
            DateTimeOffset.Now.Offset);

        if (currentCalendarSlot.Minute % 5 == 0 && _lastCalendarSyncSlot != currentCalendarSlot)
        {
            _lastCalendarSyncSlot = currentCalendarSlot;
            await SyncCalendarsAsync(showCompletionMessage: false);
        }
    }

    private void OnSummonHotkeyPressed()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => ShowQuickEntry(DateTimeOffset.Now));
    }

    private void ShowQuickEntry(DateTimeOffset promptTime, TimeEntryDraft? draft = null)
    {
        var window = new QuickEntryWindow(
            _timeTrackingService,
            promptTime,
            () => ShowDashboard(DashboardTab.Overview),
            () => ShowDashboard(DashboardTab.Settings),
            draft);

        window.Show();
        window.Activate();
    }

    private void ShowDashboard(DashboardTab tab)
    {
        _dashboardWindow ??= new MainWindow(_timeTrackingService, HandleSettingsChanged, () => SyncCalendarsAsync(showCompletionMessage: true));
        _dashboardWindow.SelectTab(tab);
        _dashboardWindow.Show();
        _dashboardWindow.WindowState = WindowState.Normal;
        _dashboardWindow.Activate();
    }

    private void HandleSettingsChanged()
    {
        ApplySettings(_timeTrackingService.LoadState().Settings);
    }

    private void ApplySettings(TimeTracker.Domain.AppSettings settings)
    {
        _startupRegistrationService.Apply(settings.LaunchAtSignIn);
        _hotkeyManager.Register(settings.SummonHotkey);
    }

    private void TogglePause()
    {
        var state = _timeTrackingService.LoadState();
        state.Settings.IsPaused = !state.Settings.IsPaused;
        _timeTrackingService.SaveSettings(state.Settings);
        ApplySettings(state.Settings);
    }

    private async Task SyncCalendarsAsync(bool showCompletionMessage)
    {
        if (_syncInProgress)
        {
            return;
        }

        _syncInProgress = true;

        try
        {
            var drafts = await _calendarSyncService.SyncCompletedMeetingsAsync(
                message => System.Windows.Application.Current.Dispatcher.Invoke(
                    () => System.Windows.MessageBox.Show(
                        message,
                        "Microsoft 365 sign-in",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information)),
                CancellationToken.None);

            foreach (var draft in drafts)
            {
                ShowQuickEntry(draft.EndTime, draft);
            }

            if (showCompletionMessage)
            {
                var message = drafts.Count == 0
                    ? "Calendar sync completed. No follow-up meetings need your input right now."
                    : $"Calendar sync completed. {drafts.Count} meeting follow-up prompt(s) were opened.";
                System.Windows.MessageBox.Show(message, "Calendar sync", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(exception.Message, "Calendar sync failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _syncInProgress = false;
        }
    }
}

public enum DashboardTab
{
    Overview = 0,
    Settings = 1,
}
