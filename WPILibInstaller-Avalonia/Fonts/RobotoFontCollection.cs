using Avalonia.Media.Fonts;

namespace WPILibInstaller.Fonts;

public sealed class RobotoFontCollection : EmbeddedFontCollection
{
    public RobotoFontCollection() : base(
        new Uri("fonts:Roboto", UriKind.Absolute),
        new Uri("avares://WPILibInstaller/Assets/Fonts", UriKind.Absolute))
    {
    }
}
