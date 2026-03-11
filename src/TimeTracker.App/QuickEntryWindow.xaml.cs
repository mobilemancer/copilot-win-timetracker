using System.Globalization;
using System.Windows;
using TimeTracker.Application;
using TimeTracker.Domain;

namespace TimeTracker.App;

public partial class QuickEntryWindow : Window
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    private readonly TimeTrackingService _timeTrackingService;
    private readonly DateTimeOffset _promptTime;
    private readonly Action _openOverview;
    private readonly Action _openSettings;
    private readonly TimeEntryDraft? _initialDraft;
    private IReadOnlyList<ProjectDefinition> _allProjects = [];

    public QuickEntryWindow(
        TimeTrackingService timeTrackingService,
        DateTimeOffset promptTime,
        Action openOverview,
        Action openSettings,
        TimeEntryDraft? initialDraft = null)
    {
        InitializeComponent();
        _timeTrackingService = timeTrackingService;
        _promptTime = promptTime;
        _openOverview = openOverview;
        _openSettings = openSettings;
        _initialDraft = initialDraft;
        Loaded += HandleLoaded;
    }

    private void HandleLoaded(object sender, RoutedEventArgs e)
    {
        var state = _timeTrackingService.LoadState();
        CustomerComboBox.ItemsSource = state.Customers;
        _allProjects = state.Projects;

        var draft = _initialDraft ?? _timeTrackingService.CreateDraft(_promptTime);
        Title = draft.PromptTitle;
        PromptTitleTextBlock.Text = draft.PromptTitle;
        StartTimeTextBox.Text = draft.StartTime.LocalDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        EndTimeTextBox.Text = draft.EndTime.LocalDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        NoteTextBox.Text = draft.Note ?? string.Empty;
        ManualCustomerTextBox.Text = draft.ManualCustomerName ?? string.Empty;
        ManualProjectTextBox.Text = draft.ManualProjectName ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(draft.SelectedCustomerName))
        {
            CustomerComboBox.SelectedItem = state.Customers.FirstOrDefault(
                customer => string.Equals(customer.Name, draft.SelectedCustomerName, StringComparison.OrdinalIgnoreCase));
        }

        BindProjects();
        if (!string.IsNullOrWhiteSpace(draft.SelectedProjectName))
        {
            ProjectComboBox.SelectedItem = _allProjects.FirstOrDefault(
                project => string.Equals(project.Name, draft.SelectedProjectName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(project.CustomerName, draft.SelectedCustomerName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void CustomerSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        BindProjects();
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!TryParseTextBox(StartTimeTextBox.Text, out var startTime)
            || !TryParseTextBox(EndTimeTextBox.Text, out var endTime))
        {
            System.Windows.MessageBox.Show(this, "Enter start and end using yyyy-MM-dd HH:mm.", "Invalid time", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedCustomer = (CustomerComboBox.SelectedItem as Customer)?.Name;
        var selectedProject = (ProjectComboBox.SelectedItem as ProjectDefinition)?.Name;
        var manualCustomer = ManualCustomerTextBox.Text.Trim();
        var manualProject = ManualProjectTextBox.Text.Trim();

        var customerName = string.IsNullOrWhiteSpace(manualCustomer) ? selectedCustomer : manualCustomer;
        var projectName = string.IsNullOrWhiteSpace(manualProject) ? selectedProject : manualProject;

        if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(projectName))
        {
            System.Windows.MessageBox.Show(this, "Choose or type both a customer and a project.", "Missing information", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _timeTrackingService.SaveEntry(new TimeEntry
            {
                Id = Guid.NewGuid(),
                StartTime = startTime,
                EndTime = endTime,
                CustomerName = customerName,
                ProjectName = projectName,
                ManualCustomerName = string.IsNullOrWhiteSpace(manualCustomer) ? null : manualCustomer,
                ManualProjectName = string.IsNullOrWhiteSpace(manualProject) ? null : manualProject,
                Note = string.IsNullOrWhiteSpace(NoteTextBox.Text) ? null : NoteTextBox.Text.Trim(),
                Source = _initialDraft?.Source ?? TimeEntrySource.Manual,
                CalendarAccountEmail = _initialDraft?.CalendarAccountEmail,
                CalendarEventId = _initialDraft?.CalendarEventId,
            });
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, "Unable to save entry", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Close();
    }

    private void SkipClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenOverviewClicked(object sender, RoutedEventArgs e)
    {
        _openOverview();
        Close();
    }

    private void OpenSettingsClicked(object sender, RoutedEventArgs e)
    {
        _openSettings();
        Close();
    }

    private void BindProjects()
    {
        var customerName = (CustomerComboBox.SelectedItem as Customer)?.Name;
        ProjectComboBox.ItemsSource = _allProjects
            .Where(project => string.Equals(project.CustomerName, customerName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool TryParseTextBox(string text, out DateTimeOffset dateTimeOffset)
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
