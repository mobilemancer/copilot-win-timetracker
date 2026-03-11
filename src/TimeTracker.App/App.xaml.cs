namespace TimeTracker.App;

public partial class App : System.Windows.Application
{
    private AppRuntime? _runtime;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        _runtime = AppRuntime.Create();
        _runtime.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _runtime?.Dispose();
        base.OnExit(e);
    }
}

