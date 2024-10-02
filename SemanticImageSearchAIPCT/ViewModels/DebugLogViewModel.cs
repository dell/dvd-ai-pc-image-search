using SemanticImageSearchAIPCT.Services;
using Serilog.Events;

namespace SemanticImageSearchAIPCT.ViewModels
{
    public partial class DebugLogViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private string logText = string.Empty;

        [ObservableProperty]
        public string title = "Debug and Tracing log";

        public DebugLogViewModel()
        {
            LoggingService.History.ForEach(e => {
                AppendLogEntry(e.LogEventLevel, e.Message);
            });
            LoggingService.LogMessage += OnLogMessageReceived;
        }

        private void OnLogMessageReceived(object? sender, LogEntry e)
        {
            if (MainThread.IsMainThread)
            {
                MainThread.BeginInvokeOnMainThread(() => { AppendLogEntry(e.LogEventLevel, e.Message); });
            }
            else
            {
                AppendLogEntry(e.LogEventLevel, e.Message);
            }
        }

        private void AppendLogEntry(LogEventLevel logLevel, string message)
        {
            switch (logLevel)
            {
                case LogEventLevel.Information:
                case LogEventLevel.Debug:
                    LogText = string.Concat(LogText, $"[{logLevel}]: ", message, Environment.NewLine);
                    break;
                default:
                    // suppress from log
                    LogText = string.Concat(LogText, $"[{logLevel}]: ", message, Environment.NewLine);
                    break;
            }
        }

        public void Dispose()
        {
            LoggingService.LogMessage -= OnLogMessageReceived;
        }
    }
}
