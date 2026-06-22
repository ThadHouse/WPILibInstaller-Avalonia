using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace WPILibShortcutCreator;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16, Options = ComInterfaceOptions.ComObjectWrapper)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal partial interface IShellLinkW
{
    [PreserveSig]
    int GetPath(IntPtr pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);

    [PreserveSig]
    int GetIDList(out IntPtr ppidl);

    [PreserveSig]
    int SetIDList(IntPtr pidl);

    [PreserveSig]
    int GetDescription(IntPtr pszName, int cchMaxName);

    [PreserveSig]
    int SetDescription(string pszName);

    [PreserveSig]
    int GetWorkingDirectory(IntPtr pszDir, int cchMaxPath);

    [PreserveSig]
    int SetWorkingDirectory(string pszDir);

    [PreserveSig]
    int GetArguments(IntPtr pszArgs, int cchMaxPath);

    [PreserveSig]
    int SetArguments(string pszArgs);

    [PreserveSig]
    int GetHotkey(out short pwHotkey);

    [PreserveSig]
    int SetHotkey(short wHotkey);

    [PreserveSig]
    int GetShowCmd(out int piShowCmd);

    [PreserveSig]
    int SetShowCmd(int iShowCmd);

    [PreserveSig]
    int GetIconLocation(IntPtr pszIconPath, int cchIconPath, out int piIcon);

    [PreserveSig]
    int SetIconLocation(string pszIconPath, int iIcon);

    [PreserveSig]
    int SetRelativePath(string pszPathRel, uint dwReserved);

    [PreserveSig]
    int Resolve(IntPtr hwnd, uint fFlags);

    [PreserveSig]
    int SetPath(string pszFile);
}
