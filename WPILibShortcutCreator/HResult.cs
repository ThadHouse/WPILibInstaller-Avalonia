namespace WPILibShortcutCreator;

internal static class HResult
{
    public static bool Succeeded(int hresult)
    {
        return hresult >= 0;
    }

    public static bool Failed(int hresult)
    {
        return hresult < 0;
    }

    public static int FromWin32(int error)
    {
        return error <= 0
            ? error
            : unchecked((int)(((uint)error & 0x0000FFFF) | 0x80070000));
    }
}
