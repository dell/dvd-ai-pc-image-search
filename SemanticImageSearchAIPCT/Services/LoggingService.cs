using Microsoft.VisualBasic;
using Serilog.Events;

namespace SemanticImageSearchAIPCT.Services
{
    public static class LoggingService
    {
        public static event EventHandler<LogEntry> LogMessage;
        public static List<LogEntry> History { get; private set; }

        private static ILogger _logger;

        public static void Initialize(ILoggerFactory loggerFactory)
        {
            History = [];
            _logger = loggerFactory.CreateLogger("GlobalLogger");
            LogMessage += OnLogMessage;
        }

        private static void OnLogMessage(object? sender, LogEntry e)
        {
            History.Add(e);
        }

        public static void LogInformation(string message)
        {
            _logger?.LogInformation(message);
            LogMessage?.Invoke(null, new LogEntry(LogEventLevel.Information, message));
   }

        public static void LogWarning(string message)
        {
            _logger?.LogWarning(message);
            LogMessage?.Invoke(null, new LogEntry(LogEventLevel.Warning, message));
        }

        public static void LogError(string message, Exception exception)
        {
            _logger?.LogError(exception, message);
            LogMessage?.Invoke(null,  new LogEntry(LogEventLevel.Error, exception.Message));
        }

        public static void LogDebug(string message)
        {
            _logger?.LogDebug(message);
            LogMessage?.Invoke(null, new LogEntry(LogEventLevel.Debug, message));
        }

        public static void LogInformation(string message, params object[] args)
        {
            var msg = string.Format(message, args);
            LogInformation(msg);
        }
    }

    public struct LogEntry
    {
        public LogEntry(LogEventLevel logEventLevel, string message) { 
            Message = message;
            LogEventLevel = logEventLevel;
        }
        public string Message { get; private set; }
        public LogEventLevel LogEventLevel { get; private set; }
    }
}
