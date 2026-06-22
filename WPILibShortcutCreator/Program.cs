using System.Runtime.InteropServices;
using System.Text.Json;
using WPILibInstaller.Models;
using WPILibInstaller.Utils;

namespace WPILibShortcutCreator;

internal static class Program
{
    private const int WpilibMissingProgramArguments = -1;
    private const int WpilibInitializationFailure = -2;
    private const int WpilibCreationFailure = -3;
    private const int WpilibSuccess = 0;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            return WpilibMissingProgramArguments;
        }

        if (!ShortcutCreator.CanCreateShellLinks())
        {
            return WpilibInitializationFailure;
        }

        ShortcutData? shortcutData = ReadShortcutData(args[0]);
        if (shortcutData is null)
        {
            return WpilibCreationFailure;
        }

        string? desktopFolder = GetSpecialFolderPath(shortcutData.IsAdmin
            ? Environment.SpecialFolder.CommonDesktopDirectory
            : Environment.SpecialFolder.DesktopDirectory);

        string? startMenuFolder = GetSpecialFolderPath(shortcutData.IsAdmin
            ? Environment.SpecialFolder.CommonStartMenu
            : Environment.SpecialFolder.StartMenu);

        bool createdDesktop = desktopFolder is not null
            && ShortcutCreator.CreateShortcuts(shortcutData.DesktopShortcuts, desktopFolder);

        bool createdStartMenu = startMenuFolder is not null
            && ShortcutCreator.CreateShortcuts(shortcutData.StartMenuShortcuts, startMenuFolder);

        return createdDesktop && createdStartMenu
            ? WpilibSuccess
            : WpilibCreationFailure;
    }

    private static ShortcutData? ReadShortcutData(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ShortcutData);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? GetSpecialFolderPath(Environment.SpecialFolder specialFolder)
    {
        string path = Environment.GetFolderPath(specialFolder);
        return path.Length == 0
            ? null
            : path;
    }
}
