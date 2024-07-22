#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System.Diagnostics;
using Windows.Graphics;
#endif

namespace SemanticImageSearchAIPCT.UI
{
    public partial class App : Application
    {
        const int WindowWidth = 450;
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
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

            if (appWindow.Presenter is OverlappedPresenter p)
            {
                p.IsResizable = true;

                // these only have effect if XAML isn't responsible for drawing the titlebar.
                p.IsMaximizable = true;
            }
#endif
            });


            MainPage = new AppShell();
        }
     
    }
}
