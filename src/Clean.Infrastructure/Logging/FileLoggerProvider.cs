using System.Globalization;
using Clean.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Logging;

// One text file per day, kept for two weeks, never sent anywhere. A day's file stops growing at 2 MB,
// so a loop that logs an error thousands of times cannot fill the disk it is meant to clean.
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const long MaximumFileBytes = 2 * 1024 * 1024;

    public static readonly TimeSpan Retention = TimeSpan.FromDays(14);

    public static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clean", "logs");

    private readonly string _folder;
    private readonly LogSanitizer _sanitizer;
    private readonly TimeProvider _clock;
    private readonly LogLevel _minimumLevel;
    private readonly object _gate = new();

    public FileLoggerProvider(string folder, LogSanitizer sanitizer, TimeProvider clock, LogLevel minimumLevel = LogLevel.Information)
    {
        _folder = folder;
        _sanitizer = sanitizer;
        _clock = clock;
        _minimumLevel = minimumLevel;
        DeleteOldFiles();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void Dispose()
    {
    }

    private void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = _clock.GetLocalNow();
        var line = $"{now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)} [{Abbreviate(level)}] {category}: {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        var text = _sanitizer.Clean(line) + Environment.NewLine;

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                var path = Path.Combine(_folder, $"clean-{now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
                if (File.Exists(path) && new FileInfo(path).Length >= MaximumFileBytes)
                {
                    return;
                }

                File.AppendAllText(path, text);
            }
            catch (Exception exception2) when (exception2 is IOException or UnauthorizedAccessException)
            {
                // Logging must never be the reason something else fails.
            }
        }
    }

    private void DeleteOldFiles()
    {
        try
        {
            if (!Directory.Exists(_folder))
            {
                return;
            }

            var limit = _clock.GetUtcNow().UtcDateTime - Retention;
            foreach (var file in new DirectoryInfo(_folder).EnumerateFiles("clean-*.log"))
            {
                if (file.LastWriteTimeUtc < limit)
                {
                    file.Delete();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Old logs staying a little longer is harmless.
        }
    }

    private static string Abbreviate(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "CRT",
    };

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= provider._minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
