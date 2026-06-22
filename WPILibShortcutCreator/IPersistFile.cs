using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace WPILibShortcutCreator;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16, Options = ComInterfaceOptions.ComObjectWrapper)]
[Guid("0000010B-0000-0000-C000-000000000046")]
internal partial interface IPersistFile
{
    [PreserveSig]
    int GetClassID(out Guid pClassID);

    [PreserveSig]
    int IsDirty();

    [PreserveSig]
    int Load(string pszFileName, uint dwMode);

    [PreserveSig]
    int Save(string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool remember);

    [PreserveSig]
    int SaveCompleted(string pszFileName);

    [PreserveSig]
    int GetCurFile(out IntPtr ppszFileName);
}
