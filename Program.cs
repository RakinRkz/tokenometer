using System.Reflection;

namespace Tokenometer;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstanceLock = new Mutex(initiallyOwned: true, "Tokenometer.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Logger.Warn("Program", "Startup aborted — another instance already holds the single-instance mutex.");
            MessageBox.Show("Tokenometer is already running — check your system tray.", "Tokenometer",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Before the first log line, so a verbose session captures startup too.
        if (new LogSettings().Verbose)
            Logger.MinimumLevel = LogLevel.Debug;

        Application.ThreadException += (_, e) =>
            Logger.Error("Program", $"Unhandled UI thread exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error("Program", $"Unhandled exception (terminating={e.IsTerminating}): {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error("Program", $"Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };

        // InformationalVersion carries <Version> plus the commit hash SourceLink
        // appends, so a support log identifies the exact build that produced it.
        string version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        Logger.Info("Program", $"Tokenometer {version} starting. PID={Environment.ProcessId}, OSVersion={Environment.OSVersion}, .NET={Environment.Version}");
        Logger.Debug("Program", $"Log file: {Logger.LogFilePath}");

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());

        Logger.Info("Program", "Tokenometer exiting normally.");
    }
}
