
using CommunityToolkit.Maui;
using SemanticImageSearchAIPCT.UI.Controls;
using SemanticImageSearchAIPCT.UI.Services;
using System.Diagnostics;

namespace SemanticImageSearchAIPCT.UI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {

                Debug.WriteLine($"Global exception caught: {e.ExceptionObject}");
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

            //builder.Services.AddSingleton<MainPage>();

            var app = builder.Build();

            ServiceHelper.Initialize(app.Services);
                        
            ServiceProvider = app.Services;

            return app;
        }

        public static IServiceProvider ServiceProvider { get; private set; }
    }
}
