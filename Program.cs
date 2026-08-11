namespace Tokenometer;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstanceLock = new Mutex(initiallyOwned: true, "Tokenometer.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Logger.Log("Program", "Startup aborted — another instance already holds the single-instance mutex.");
            MessageBox.Show("Tokenometer is already running — check your system tray.", "Tokenometer",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.ThreadException += (_, e) =>
            Logger.Log("Program", $"Unhandled UI thread exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Log("Program", $"Unhandled exception (terminating={e.IsTerminating}): {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Log("Program", $"Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };

        Logger.Log("Program", $"Tokenometer starting. PID={Environment.ProcessId}, OSVersion={Environment.OSVersion}, .NET={Environment.Version}");
        Logger.Log("Program", $"Log file: {Logger.LogFilePath}");

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());

        Logger.Log("Program", "Tokenometer exiting normally.");
    }
}
