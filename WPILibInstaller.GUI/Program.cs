using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using WPILibInstaller.Fonts;

namespace WPILibInstaller
{
    sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFontCollection(new RobotoFontCollection());
                })
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "avares://WPILibInstaller/Assets/Fonts#Roboto"
                })
                .With(new CompositionOptions()
                {
                    UseRegionDirtyRectClipping = false
                })
                .LogToTrace();
        }
    }
}
