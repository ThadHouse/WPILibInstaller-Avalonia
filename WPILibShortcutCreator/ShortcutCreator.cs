using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using WPILibInstaller.Models;

namespace WPILibShortcutCreator;

internal static class ShortcutCreator
{
    private static readonly StrategyBasedComWrappers ComWrappers = new();

    public static bool CanCreateShellLinks()
    {
        try
        {
            object? shellLink = CreateShellLinkObject();
            return shellLink is IShellLinkW and IPersistFile;
        }
        catch (COMException)
        {
            return false;
        }
    }

    public static bool CreateShortcuts(IReadOnlyList<ShortcutInfo> shortcuts, string destination)
    {
        bool allCompleted = true;

        foreach (ShortcutInfo shortcut in shortcuts)
        {
            if (!CreateShortcut(destination, shortcut))
            {
                allCompleted = false;
            }
        }

        return allCompleted;
    }

    private static bool CreateShortcut(string destination, ShortcutInfo shortcutInfo)
    {
        try
        {
            object? shellLinkObject = CreateShellLinkObject();
            if (shellLinkObject is not IShellLinkW shellLink
                || shellLinkObject is not IPersistFile persistFile)
            {
                return false;
            }

            if (HResult.Failed(shellLink.SetPath(shortcutInfo.Path))
                || HResult.Failed(shellLink.SetDescription(shortcutInfo.Description)))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(shortcutInfo.IconLocation)
                && HResult.Failed(shellLink.SetIconLocation(shortcutInfo.IconLocation, 0)))
            {
                return false;
            }

            string finalPath = Path.Combine(destination, shortcutInfo.Name + ".lnk");
            int hresult = persistFile.Save(finalPath, remember: true);

            if (hresult == HResult.FromWin32(NativeMethods.ErrorPathNotFound)
                && TryCreateParentDirectory(finalPath))
            {
                hresult = persistFile.Save(finalPath, remember: true);
            }

            return HResult.Succeeded(hresult);
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    private static object? CreateShellLinkObject()
    {
        Guid classId = ComGuids.ShellLinkClassId;
        Guid interfaceId = typeof(IShellLinkW).GUID;
        int hresult = NativeMethods.CoCreateInstance(
            ref classId,
            IntPtr.Zero,
            NativeMethods.ClsctxInprocServer,
            ref interfaceId,
            out IntPtr shellLinkPointer);

        if (HResult.Failed(hresult))
        {
            return null;
        }

        try
        {
            return ComWrappers.GetOrCreateObjectForComInstance(
                shellLinkPointer,
                CreateObjectFlags.UniqueInstance);
        }
        finally
        {
            _ = Marshal.Release(shellLinkPointer);
        }
    }

    private static bool TryCreateParentDirectory(string filePath)
    {
        try
        {
            string? parentDirectory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(parentDirectory))
            {
                return false;
            }

            Directory.CreateDirectory(parentDirectory);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
