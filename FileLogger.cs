using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace FireWallService
{
    /// <summary>
    /// Простой файловый логгер (без консоли)
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logFilePath;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new();
        private StreamWriter? _writer;

        public FileLoggerProvider(string logFilePath, LogLevel minLevel = LogLevel.Debug)
        {
            _logFilePath = logFilePath;
            _minLevel = minLevel;

            var dir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _writer = new StreamWriter(_logFilePath, append: true);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_writer, _lock, categoryName, _minLevel);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }
    }

    public class FileLogger : ILogger
    {
        private readonly StreamWriter? _writer;
        private readonly object _lock;
        private readonly string _category;
        private readonly LogLevel _minLevel;

        public FileLogger(StreamWriter? writer, object lockObj, string category, LogLevel minLevel)
        {
            _writer = writer;
            _lock = lockObj;
            _category = category;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel,-13}] [{_category}] {message}";
            if (exception != null)
                line += $"\n  Exception: {exception}";

            lock (_lock)
            {
                try
                {
                    _writer?.WriteLine(line);
                    _writer?.Flush();
                }
                catch { /* игнорируем ошибки записи */ }
            }
        }
    }

    /// <summary>
    /// Extension для удобного добавления FileLogger
    /// </summary>
    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string logFilePath, LogLevel minLevel = LogLevel.Debug)
        {
            builder.AddProvider(new FileLoggerProvider(logFilePath, minLevel));
            return builder;
        }
    }
}
