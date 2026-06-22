using System.Runtime.InteropServices;

namespace WPILibShortcutCreator;

internal static partial class NativeMethods
{
    public const uint ClsctxInprocServer = 0x1;
    public const int ErrorPathNotFound = 3;

    [LibraryImport("ole32", EntryPoint = "CoCreateInstance")]
    public static partial int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);
}
