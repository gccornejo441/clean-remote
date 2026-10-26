namespace CleanTVRemote;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppLog.Error("Unhandled Windows Forms exception.", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                AppLog.Error("Unhandled application exception.", exception);
            else
                AppLog.Info($"Unhandled application error: {e.ExceptionObject}");
        };

        ApplicationConfiguration.Initialize();
        AppLog.Info("Application started.");
        Application.Run(new MainForm());
    }
}
