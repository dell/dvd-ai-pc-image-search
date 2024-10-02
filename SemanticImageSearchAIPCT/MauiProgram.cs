
using CommunityToolkit.Maui;
using SemanticImageSearchAIPCT.Controls;
using SemanticImageSearchAIPCT.Services;
using System.Diagnostics;
using Serilog;
using System.Configuration;

namespace SemanticImageSearchAIPCT
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                LoggingService.LogDebug($"Global exception caught: {e.ExceptionObject}");
            };

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<IClipInferenceService, ClipInferenceService>();
            builder.Services.AddSingleton<IWhisperEncoderInferenceService, WhisperEncoderInferenceService>();
            builder.Services.AddSingleton<IWhisperDecoderInferenceService, WhisperDecoderInferenceService>();
            builder.Services.AddSingleton<ImportImagesViewModel>();
            builder.Services.AddSingleton<SearchViewModel>();
            builder.Services.AddSingleton<ImportFolderView>();
            builder.Services.AddSingleton<SearchPage>();

            EnsureLogsDirectoryExists();
            ConfigureLogging(builder);

            Log.Information("MauiProgram started");


            var app = builder.Build();

            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            LoggingService.Initialize(loggerFactory);

            ServiceHelper.Initialize(app.Services);

            ServiceProvider = app.Services;

            return app;
        }

        private static void EnsureLogsDirectoryExists()
        {
            try
            {
                var logDirectory = ConfigurationManager.AppSettings["LogFilePath"];
                if (string.IsNullOrWhiteSpace(logDirectory) == false)
                {
                    Debug.WriteLine($"Checking directory: {logDirectory}");

                    if (Directory.Exists(logDirectory))
                    {
                        Debug.WriteLine($"Directory already exists: {logDirectory}");
                    }
                    else
                    {
                        Directory.CreateDirectory(logDirectory);
                        Debug.WriteLine($"Created directory: {logDirectory}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in EnsureLogsDirectoryExists: {ex.Message}");
            }
        }

        private static void ConfigureLogging(MauiAppBuilder builder)
        {
            try
            {
                // Read configurations from app.config
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["LogFilePath"] ?? "AIPCT_Logs");
                var minimumLogLevel = ConfigurationManager.AppSettings["MinimumLogLevel"];

                // Ensure the directory exists
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Set up the minimum log level
                var levelSwitch = new Serilog.Core.LoggingLevelSwitch();
                switch (minimumLogLevel)
                {
                    case "Verbose":
                        levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
                        break;
                    case "Debug":
                        levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
                        break;
                    case "Information":
                        levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
                        break;
                    case "Warning":
                        levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Warning;
                        break;
                    case "Error":
                        levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Error;
                        break;
                    case "Fatal":
                        levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Fatal;
                        break;
                    default:
                        levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
                        break;
                }

                // Combine log directory with log filename
                var logFilePath = Path.Combine(logDirectory, "log.txt");

                // Configure Serilog
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(levelSwitch)
                    .WriteTo.File(
                        path: logFilePath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}",
                        retainedFileCountLimit: null  // Optional: retain all log files
                    )
                    .WriteTo.Debug()
                    .CreateLogger();

                // Set up logging with Serilog
                builder.Logging.ClearProviders();
                builder.Logging.AddSerilog(Log.Logger, dispose: true);

                Log.Information("Logging configured successfully.");
            }
            catch (Exception ex)
            {
                LoggingService.LogDebug($"Error in ConfigureLogging: {ex.Message}");
            }
        }


        public static IServiceProvider ServiceProvider { get; private set; }
    }
}
