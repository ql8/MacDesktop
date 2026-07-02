using System.Text;

namespace MacDesktop;

/// <summary>极简文件日志，写入 %TEMP%\MacDesktop.log 便于排查问题。</summary>
internal static class Log
{
    private static readonly object Sync = new();

    public static string FilePath { get; } =
        Path.Combine(Path.GetTempPath(), "MacDesktop.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
                File.AppendAllText(
                    FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}",
                    Encoding.UTF8);
        }
        catch
        {
            /* 日志失败不影响主流程 */
        }
    }

    public static void Error(string context, Exception ex)
        => Write($"ERROR {context}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex}");
}
