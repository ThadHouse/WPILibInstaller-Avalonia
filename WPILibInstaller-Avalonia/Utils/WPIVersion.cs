using System.Collections.Generic;

namespace WPILibInstaller.Utils
{
    public class WPIVersion
    {
        public int Major { get; set; } = -1;
        public int Minor { get; set; } = -1;
        public int Patch { get; set; } = -1;
        public string? Else { get; set; }
        public string? FullVersion { get; set; }

        public WPIVersion(string version)
        {
            var dashIndex = version.IndexOf('-');
            var dashParts = new List<string>();
            if (dashIndex > 0)
            {
                dashParts.Add(version.Substring(0, dashIndex));
                dashParts.Add(version.Substring(dashIndex + 1));
            }
            else
            {
                dashParts.Add(version);
            }

            var parts = version.Split('.');

            FullVersion = version;

            Else = dashParts.Count == 2 ? dashParts[1] : null;
            if (parts.Length > 0)
            {
                if (int.TryParse(parts[0], out var major))
                {
                    Major = major;
                }
            }
            if (parts.Length > 1)
            {
                if (int.TryParse(parts[1], out var minor))
                {
                    Minor = minor;
                }
            }
            if (parts.Length > 2)
            {
                if (int.TryParse(parts[2], out var patch))
                {
                    Patch = patch;
                }
            }
        }

        public static bool operator >(WPIVersion version1, WPIVersion version2)
        {
            if (version1.FullVersion == version2.FullVersion)
            {
                return false;
            }

            if (version1.Major > version2.Major)
            {
                return true;
            }
            else if (version1.Major < version2.Major)
            {
                return false;
            }

            if (version1.Minor > version2.Minor)
            {
                return true;
            }
            else if (version1.Minor < version2.Minor)
            {
                return false;
            }

            if (version1.Patch > version2.Patch)
            {
                return true;
            }
            else if (version1.Patch < version2.Patch)
            {
                return false;
            }

            // At this point, major, minor and patch are equal.
            // If a has no extra data, it is greater.
            // If b has no extra data, it is not greater

            if (version1.Else == null)
            {
                return true;
            }
            else if (version2.Else == null)
            {
                return false;
            }

            // If both have extra data, check for pre,
            // then go alphanumeric
            var version1Pre = version1.Else.Contains("pre");
            var version2Pre = version2.Else.Contains("pre");

            if (version1Pre && version2Pre)
            {
                // Both pre, return greatest
                return version1.Else.CompareTo(version2.Else, StringComparison.Ordinal) > 0;
            }
            else if (version1Pre)
            {
                // False if 1 has pre
                return false;
            }
            else if (version2Pre)
            {
                // True is 2 has pre
                return true;
            }
            else
            {
                // Neither has pre, return greatest.
                return version1.Else.CompareTo(version2.Else, StringComparison.Ordinal) > 0;
            }
        }

        public static bool operator <(WPIVersion version1, WPIVersion version2)
        {
            return !(version1 > version2);
        }
    }
}
