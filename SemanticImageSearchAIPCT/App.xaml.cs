#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using SemanticImageSearchAIPCT.Services;
using System.Diagnostics;
using Windows.Graphics;
#endif

namespace SemanticImageSearchAIPCT
{
    public partial class App : Application
    {
        const int WindowWidth = 1200;
        const int WindowHeight = 800;

        public App()
        {
            InitializeComponent();
           
            Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
            {
#if WINDOWS
                var mauiWindow = handler.VirtualView;
                var nativeWindow = handler.PlatformView;
                nativeWindow.Activate();
                IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
                var dpi = nativeWindow.GetDisplayDensity();
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.Resize(new SizeInt32((int)(WindowWidth * dpi), (int)(WindowHeight * dpi)));
                appWindow.Move(new PointInt32(300, 50));

                if (appWindow.Presenter is OverlappedPresenter p)
                {
                    p.IsResizable = true;

                    // these only have effect if XAML isn't responsible for drawing the titlebar.
                    p.IsMaximizable = true;
                }
#endif
            });

            InitializeModels();
            MainPage = new AppShell();
        }
        private void InitializeModels()
        {
            Task.Run(async () =>
            {
                try
                {
                    var encoderService = MauiProgram.ServiceProvider.GetService<IWhisperEncoderInferenceService>();
                    var decoderService = MauiProgram.ServiceProvider.GetService<IWhisperDecoderInferenceService>();

                    if (encoderService != null && decoderService != null)
                    {
                        await encoderService.InitializeEncoderModel();
                        await decoderService.InitializeDecoderModel();
                    }
                }
                catch (Exception ex)
                {                
                    LoggingService.LogError("Error while InitializeModels:", ex);
                }
            });
        }

    }
}
