using SemanticImageSearchAIPCT.Services;
using Serilog.Events;

namespace SemanticImageSearchAIPCT.ViewModels
{
    public partial class StatusFooterViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private string statusText = string.Empty;

        public StatusFooterViewModel() {
            LoggingService.LogMessage += OnLogMessageReceived;
        }

        private void OnLogMessageReceived(object? sender, LogEntry e)
        {
            if (MainThread.IsMainThread)
            {
                MainThread.BeginInvokeOnMainThread(() => { UpdateFooterText(e.LogEventLevel, e.Message); });
            }
            else
            {
                UpdateFooterText(e.LogEventLevel, e.Message);
            }
        }

        private void UpdateFooterText(LogEventLevel logLevel, string message)
        {
            switch (logLevel)
            {
                case LogEventLevel.Information:
                    StatusText = message;
                    break;
                default:
                    // suppress from footer
                    break;
            }
        }

        public void Dispose()
        {
            LoggingService.LogMessage -= OnLogMessageReceived;
        }
    }
}
