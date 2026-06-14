using System.IO;

namespace MC_Helper.Helpers;

public static class Logger
{
    private static readonly string LogDir = Path.Combine(
        AppContext.BaseDirectory, "MC_Helper_config");

    private static readonly string LogPath = Path.Combine(LogDir, "log.txt");

    private static readonly object _lock = new();

    static Logger()
    {
        try { Directory.CreateDirectory(LogDir); } catch { }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            try { File.Delete(LogPath); } catch { }
        }
    }

    public static void Info(string msg) => Log("INFO", msg);
    public static void Error(string msg, Exception? ex = null) =>
        Log("ERROR", ex != null ? $"{msg}\n{ex}" : msg);

    private static void Log(string level, string msg)
    {
        lock (_lock)
        {
            try
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}\n";
                File.AppendAllText(LogPath, line);
            }
            catch { }
        }
    }
}
