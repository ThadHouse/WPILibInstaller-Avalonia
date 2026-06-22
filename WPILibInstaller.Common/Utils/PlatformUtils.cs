using System.Runtime.InteropServices;

namespace WPILibInstaller.Utils
{
    public enum Platform
    {
        Win64,
        Linux64,
        LinuxArm64,
        Mac64,
        MacArm64,
        Invalid
    }

    public class PlatformUtils
    {
        static PlatformUtils()
        {
            CurrentPlatform = Platform.Invalid;

            var currentArch = RuntimeInformation.OSArchitecture;
            if (currentArch != Architecture.X64 && currentArch != Architecture.Arm64)
            {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (currentArch == Architecture.X64)
                {
                    CurrentPlatform = Platform.Linux64;
                }
                else if (currentArch == Architecture.Arm64)
                {
                    CurrentPlatform = Platform.LinuxArm64;
                }
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (currentArch == Architecture.X64)
                {
                    CurrentPlatform = Platform.Mac64;
                }
                else
                {
                    CurrentPlatform = Platform.MacArm64;
                }
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Since this program ships with an x64 .NET runtime, if it is running on ARM64
                // we can safely assume that x64 emulation is available and working. This means
                // everything else (except kernel drivers) can run as x64 ("Win64" in the local
                // Platform enum).
                if (currentArch == Architecture.X64 || currentArch == Architecture.Arm64)
                {
                    CurrentPlatform = Platform.Win64;
                }
                return;
            }
        }

        public static Platform CurrentPlatform { get; }
    }
}
