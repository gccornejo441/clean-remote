using System.Text.RegularExpressions;

namespace CleanTVRemote;

internal static class AppLog
{
    private static readonly object Gate = new();

    public static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CleanTVRemote",
        "app.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                string? directory = Path.GetDirectoryName(LogFilePath);
                if (directory == null) return;
                Directory.CreateDirectory(directory);

                var file = new FileInfo(LogFilePath);
                if (file.Exists && file.Length > 2 * 1024 * 1024)
                {
                    string previousPath = LogFilePath + ".1";
                    if (File.Exists(previousPath)) File.Delete(previousPath);
                    File.Move(LogFilePath, previousPath);
                }

                string entry = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
                if (exception != null)
                {
                    string details = Regex.Replace(exception.ToString(), "(?i)(token=)[^&\\s\\\"']+", "$1[REDACTED]");
                    entry += details + Environment.NewLine;
                }
                File.AppendAllText(LogFilePath, entry + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never prevent the remote from running.
        }
    }
}
