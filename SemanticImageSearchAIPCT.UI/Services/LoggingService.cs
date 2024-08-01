namespace SemanticImageSearchAIPCT.UI.Services
{
    public static class LoggingService
    {
        private static ILogger _logger;

        public static void Initialize(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger("GlobalLogger");
        }

        public static void LogInformation(string message)
        {
            _logger?.LogInformation(message);
        }

        public static void LogWarning(string message)
        {
            _logger?.LogWarning(message);
        }

        public static void LogError(string message, Exception exception)
        {
            _logger?.LogError(exception, message);
        }

        public static void LogDebug(string message)
        {
            _logger?.LogDebug(message);
        }

        public static void LogInformation(string message, params object[] args)
        {
            _logger?.LogInformation(message, args);
        }
    }
}
