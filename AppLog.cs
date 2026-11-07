using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace CleanTVRemote;

internal static class AppLog
{
    private const long MaxFileBytes = 2 * 1024 * 1024;
    private const int MaxEntryChars = 8192;

    private static readonly Channel<string> Entries = Channel.CreateBounded<string>(
        new BoundedChannelOptions(64)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private static readonly Regex TokenPattern = new(
        @"(\b(?:token|access_token|refresh_token)\b['""]?\s*[:=]\s*['""]?)[^&\s,'""}]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Task WriterTask = Task.Run(ProcessEntriesAsync);

    public static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CleanTVRemote",
        "app.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    public static void Shutdown()
    {
        try
        {
            Entries.Writer.TryComplete();
            WriterTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            string entry = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
            if (exception != null)
                entry += exception + Environment.NewLine;

            if (entry.Length > MaxEntryChars)
                entry = entry[..MaxEntryChars] + "... [TRUNCATED]" + Environment.NewLine;

            entry = TokenPattern.Replace(entry, "$1[REDACTED]");
            Entries.Writer.TryWrite(entry + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static async Task ProcessEntriesAsync()
    {
        await foreach (string entry in Entries.Reader.ReadAllAsync())
        {
            try
            {
                string path = LogFilePath;
                string? directory = Path.GetDirectoryName(path);
                if (directory == null)
                    continue;

                Directory.CreateDirectory(directory);

                var file = new FileInfo(path);
                if (file.Exists && file.Length + Encoding.UTF8.GetByteCount(entry) > MaxFileBytes)
                {
                    string previousPath = path + ".1";
                    File.Move(path, previousPath, true);
                }

                await File.AppendAllTextAsync(path, entry, Encoding.UTF8).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
