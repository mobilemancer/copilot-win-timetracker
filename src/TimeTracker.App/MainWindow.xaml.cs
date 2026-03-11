using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using TimeTracker.Application;
using TimeTracker.Domain;

namespace TimeTracker.App;

public partial class MainWindow : Window
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";
    private readonly TimeTrackingService _timeTrackingService;
    private readonly Action _settingsChanged;
    private readonly Func<Task> _syncCalendars;

    private readonly ObservableCollection<EditableEntryRow> _entryRows = [];
    private readonly ObservableCollection<CustomerRow> _customerRows = [];
    private readonly ObservableCollection<ProjectRow> _projectRows = [];
    private readonly ObservableCollection<AccountRow> _accountRows = [];
    private readonly ObservableCollection<TimelineBlockRow> _timelineRows = [];

    public MainWindow(TimeTrackingService timeTrackingService, Action settingsChanged, Func<Task> syncCalendars)
    {
        InitializeComponent();
        _timeTrackingService = timeTrackingService;
        _settingsChanged = settingsChanged;
        _syncCalendars = syncCalendars;

        EntriesDataGrid.ItemsSource = _entryRows;
        CustomersDataGrid.ItemsSource = _customerRows;
        ProjectsDataGrid.ItemsSource = _projectRows;
        AccountsDataGrid.ItemsSource = _accountRows;
        TimelineItemsControl.ItemsSource = _timelineRows;

        OverviewDatePicker.SelectedDate = DateTime.Today;
        Loaded += (_, _) => ReloadAll();
        Closing += HandleClosing;
    }

    public void SelectTab(DashboardTab tab)
    {
        RootTabControl.SelectedIndex = (int)tab;
        ReloadAll();
    }

    private void HandleClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void OverviewModeChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshOverview();
    }

    private void OverviewDateChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshOverview();
    }

    private void TodayClicked(object sender, RoutedEventArgs e)
    {
        OverviewDatePicker.SelectedDate = DateTime.Today;
        RefreshOverview();
    }

    private void RefreshClicked(object sender, RoutedEventArgs e)
    {
        ReloadAll();
    }

    private void SaveEntriesClicked(object sender, RoutedEventArgs e)
    {
        var entries = new List<TimeEntry>();
        foreach (var row in _entryRows)
        {
            if (!TryParseEntryDateTime(row.StartTimeText, out var startTime)
                || !TryParseEntryDateTime(row.EndTimeText, out var endTime))
            {
                System.Windows.MessageBox.Show(this, $"Invalid date/time in entry '{row.CustomerName} / {row.ProjectName}'.", "Invalid value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            entries.Add(new TimeEntry
            {
                Id = row.Id,
                StartTime = startTime,
                EndTime = endTime,
                CustomerName = row.CustomerName.Trim(),
                ProjectName = row.ProjectName.Trim(),
                Note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note.Trim(),
                Source = row.Source,
                ManualCustomerName = row.ManualCustomerName,
                ManualProjectName = row.ManualProjectName,
                CalendarAccountEmail = row.CalendarAccountEmail,
                CalendarEventId = row.CalendarEventId,
            });
        }

        try
        {
            _timeTrackingService.SaveEntries(entries);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, "Unable to save entries", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        ReloadAll();
    }

    private void AddCustomerClicked(object sender, RoutedEventArgs e)
    {
        _customerRows.Add(new CustomerRow());
    }

    private void RemoveCustomerClicked(object sender, RoutedEventArgs e)
    {
        if (CustomersDataGrid.SelectedItem is CustomerRow selected)
        {
            _customerRows.Remove(selected);
        }
    }

    private void AddProjectClicked(object sender, RoutedEventArgs e)
    {
        _projectRows.Add(new ProjectRow
        {
            CustomerName = _customerRows.FirstOrDefault()?.Name ?? "Internal",
        });
    }

    private void RemoveProjectClicked(object sender, RoutedEventArgs e)
    {
        if (ProjectsDataGrid.SelectedItem is ProjectRow selected)
        {
            _projectRows.Remove(selected);
        }
    }

    private void AddAccountClicked(object sender, RoutedEventArgs e)
    {
        _accountRows.Add(new AccountRow());
    }

    private void RemoveAccountClicked(object sender, RoutedEventArgs e)
    {
        if (AccountsDataGrid.SelectedItem is AccountRow selected)
        {
            _accountRows.Remove(selected);
        }
    }

    private async void SyncCalendarsClicked(object sender, RoutedEventArgs e)
    {
        await _syncCalendars();
    }

    private void SaveSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (!TimeSpan.TryParse(WorkDayStartTextBox.Text, out var workDayStart)
            || !TimeSpan.TryParse(WorkDayEndTextBox.Text, out var workDayEnd))
        {
            System.Windows.MessageBox.Show(this, "Working hours must use HH:mm format.", "Invalid working hours", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var customers = _customerRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new Customer
            {
                Name = row.Name.Trim(),
                IsInternal = row.IsInternal,
            })
            .ToList();

        var projects = _projectRows
            .Where(row => !string.IsNullOrWhiteSpace(row.CustomerName) && !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new ProjectDefinition
            {
                CustomerName = row.CustomerName.Trim(),
                Name = row.Name.Trim(),
            })
            .ToList();

        var accounts = _accountRows
            .Where(row => !string.IsNullOrWhiteSpace(row.DisplayName))
            .Select(row => new Office365AccountSettings
            {
                DisplayName = row.DisplayName.Trim(),
                TenantId = row.TenantId.Trim(),
                ClientId = row.ClientId.Trim(),
                Enabled = row.Enabled,
            })
            .ToList();

        _timeTrackingService.SaveMasterData(customers, projects);
        _timeTrackingService.SaveSettings(new AppSettings
        {
            WorkDayStart = workDayStart,
            WorkDayEnd = workDayEnd,
            IsPaused = PausedCheckBox.IsChecked == true,
            LaunchAtSignIn = LaunchAtSignInCheckBox.IsChecked == true,
            SummonHotkey = HotkeyTextBox.Text.Trim(),
            ReminderIntervalMinutes = 30,
            Office365Accounts = accounts,
        });

        _settingsChanged();
        ReloadAll();
    }

    private void ReloadAll()
    {
        var state = _timeTrackingService.LoadState();

        _customerRows.Clear();
        foreach (var customer in state.Customers)
        {
            _customerRows.Add(new CustomerRow
            {
                Name = customer.Name,
                IsInternal = customer.IsInternal,
            });
        }

        _projectRows.Clear();
        foreach (var project in state.Projects)
        {
            _projectRows.Add(new ProjectRow
            {
                CustomerName = project.CustomerName,
                Name = project.Name,
            });
        }

        _accountRows.Clear();
        foreach (var account in state.Settings.Office365Accounts)
        {
            _accountRows.Add(new AccountRow
            {
                Enabled = account.Enabled,
                DisplayName = account.DisplayName,
                TenantId = account.TenantId,
                ClientId = account.ClientId,
            });
        }

        WorkDayStartTextBox.Text = state.Settings.WorkDayStart.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        WorkDayEndTextBox.Text = state.Settings.WorkDayEnd.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        HotkeyTextBox.Text = state.Settings.SummonHotkey;
        PausedCheckBox.IsChecked = state.Settings.IsPaused;
        LaunchAtSignInCheckBox.IsChecked = state.Settings.LaunchAtSignIn;

        RefreshOverview();
    }

    private void RefreshOverview()
    {
        var state = _timeTrackingService.LoadState();
        var selectedDate = OverviewDatePicker.SelectedDate ?? DateTime.Today;
        var mode = ((OverviewModeComboBox.SelectedItem as ComboBoxItem)?.Content as string) ?? "Day";

        var filteredEntries = mode == "Week"
            ? GetWeekEntries(state.Entries, selectedDate)
            : state.Entries.Where(entry => entry.StartTime.LocalDateTime.Date == selectedDate.Date).ToList();

        _entryRows.Clear();
        foreach (var entry in filteredEntries.OrderBy(entry => entry.StartTime))
        {
            _entryRows.Add(new EditableEntryRow
            {
                Id = entry.Id,
                StartTimeText = entry.StartTime.LocalDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                EndTimeText = entry.EndTime.LocalDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                CustomerName = entry.CustomerName,
                ProjectName = entry.ProjectName,
                Note = entry.Note ?? string.Empty,
                Source = entry.Source,
                ManualCustomerName = entry.ManualCustomerName,
                ManualProjectName = entry.ManualProjectName,
                CalendarAccountEmail = entry.CalendarAccountEmail,
                CalendarEventId = entry.CalendarEventId,
            });
        }

        _timelineRows.Clear();
        foreach (var entry in filteredEntries.OrderBy(entry => entry.StartTime))
        {
            var durationMinutes = Math.Max(30, (entry.EndTime - entry.StartTime).TotalMinutes);
            _timelineRows.Add(new TimelineBlockRow
            {
                Width = durationMinutes * 4,
                Title = $"{entry.CustomerName} / {entry.ProjectName}",
                Subtitle = $"{entry.StartTime.LocalDateTime:ddd HH:mm} - {entry.EndTime.LocalDateTime:HH:mm}",
            });
        }
    }

    private static List<TimeEntry> GetWeekEntries(IEnumerable<TimeEntry> entries, DateTime selectedDate)
    {
        var diff = ((int)selectedDate.DayOfWeek + 6) % 7;
        var weekStart = selectedDate.Date.AddDays(-diff);
        var weekEnd = weekStart.AddDays(7);

        return entries
            .Where(entry =>
            {
                var start = entry.StartTime.LocalDateTime.Date;
                return start >= weekStart && start < weekEnd;
            })
            .ToList();
    }

    private static bool TryParseEntryDateTime(string text, out DateTimeOffset dateTimeOffset)
    {
        if (DateTime.TryParseExact(
            text,
            DateTimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var dateTime))
        {
            dateTimeOffset = new DateTimeOffset(dateTime);
            return true;
        }

        dateTimeOffset = default;
        return false;
    }
}

public sealed class EditableEntryRow : NotifyBase
{
    private string? _calendarAccountEmail;
    private string? _calendarEventId;
    private string _customerName = string.Empty;
    private string _endTimeText = string.Empty;
    private string? _manualCustomerName;
    private string? _manualProjectName;
    private string _note = string.Empty;
    private string _projectName = string.Empty;
    private string _startTimeText = string.Empty;

    public Guid Id { get; set; }

    public TimeEntrySource Source { get; set; }

    public string? ManualCustomerName
    {
        get => _manualCustomerName;
        set => SetProperty(ref _manualCustomerName, value);
    }

    public string? ManualProjectName
    {
        get => _manualProjectName;
        set => SetProperty(ref _manualProjectName, value);
    }

    public string? CalendarAccountEmail
    {
        get => _calendarAccountEmail;
        set => SetProperty(ref _calendarAccountEmail, value);
    }

    public string? CalendarEventId
    {
        get => _calendarEventId;
        set => SetProperty(ref _calendarEventId, value);
    }

    public string StartTimeText
    {
        get => _startTimeText;
        set => SetProperty(ref _startTimeText, value);
    }

    public string EndTimeText
    {
        get => _endTimeText;
        set => SetProperty(ref _endTimeText, value);
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string ProjectName
    {
        get => _projectName;
        set => SetProperty(ref _projectName, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }
}

public sealed class CustomerRow : NotifyBase
{
    private bool _isInternal;
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsInternal
    {
        get => _isInternal;
        set => SetProperty(ref _isInternal, value);
    }
}

public sealed class ProjectRow : NotifyBase
{
    private string _customerName = string.Empty;
    private string _name = string.Empty;

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}

public sealed class AccountRow : NotifyBase
{
    private string _clientId = string.Empty;
    private string _displayName = string.Empty;
    private string _tenantId = string.Empty;
    private bool _enabled = true;

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string TenantId
    {
        get => _tenantId;
        set => SetProperty(ref _tenantId, value);
    }

    public string ClientId
    {
        get => _clientId;
        set => SetProperty(ref _clientId, value);
    }
}

public sealed class TimelineBlockRow : NotifyBase
{
    private string _subtitle = string.Empty;
    private string _title = string.Empty;
    private double _width;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }
}

public abstract class NotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
