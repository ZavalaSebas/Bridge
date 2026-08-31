using System.IO;
using System.Text;

namespace Bridge.Services;

/// <summary>
/// Lightweight, dependency-free diagnostic logger. Bridge ships as a
/// self-contained desktop app that can't be attached to a debugger in the
/// field, so a plain rolling log file is the only way to find out why something
/// failed on a user's machine.
///
/// <para>
/// Every method is best-effort and must <b>never throw</b>: a logging failure
/// can't be allowed to cause the very crash it was meant to record. This
/// generalizes the former one-off <c>App.LogException</c> (which only wrote
/// unhandled UI-thread exceptions to <c>errors.log</c>) into a shared facade
/// used across services, including the swallowed-exception paths in the
/// settings stores where failures used to disappear entirely.
/// </para>
/// </summary>
internal static class AppLog
{
    private static readonly object Gate = new();

    // Rotate at ~1 MB and keep a single previous file (bridge.log.1). A game
    // library manager doesn't need audit-grade retention — just enough recent
    // history to explain the last session's failures without growing unbounded.
    private const long MaxBytes = 1024 * 1024;

    internal enum Level
    {
        Info,
        Warn,
        Error
    }

    public static void Info(string message) => Write(Level.Info, message, null);

    public static void Warn(string message, Exception? exception = null) =>
        Write(Level.Warn, message, exception);

    public static void Error(string message, Exception? exception = null) =>
        Write(Level.Error, message, exception);

    private static void Write(Level level, string message, Exception? exception)
    {
        try
        {
            var builder = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [")
                .Append(level.ToString().ToUpperInvariant())
                .Append("] ")
                .Append(message);

            // Full ToString() (type + message + stack) on its own lines so a
            // stack trace stays readable and greppable, not flattened into one.
            if (exception is not null)
            {
                builder.Append(Environment.NewLine);
                builder.Append(exception);
            }

            AppendLine(builder.ToString());
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    private static void AppendLine(string text)
    {
        lock (Gate)
        {
            var path = Config.LogFilePath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            RotateIfNeeded(path);
            File.AppendAllText(path, text + Environment.NewLine);
        }
    }

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxBytes)
            return;

        var backup = path + ".1";
        if (File.Exists(backup))
            File.Delete(backup);

        File.Move(path, backup);
    }
}
