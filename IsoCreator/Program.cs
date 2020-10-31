using System;
using System.IO;
using DiscUtils;
using DiscUtils.Iso9660;
using DiscUtils.Raw;

namespace IsoCreator
{
    class Program
    {
        static int Main(string? input = null, string? output = null, string? version = null)
        {
            if (input == null)
            {
                Console.WriteLine("Input is required");
                return 1;
            }

            if (output == null)
            {
                Console.WriteLine("Output is required");
                return 1;
            }

            if (version == null)
            {
                version = "unknown";
            }

            if (!Directory.Exists(input))
            {
                Console.WriteLine("Input must be a directory and must exist");
                return 1;
            }

            CDBuilder builder = new CDBuilder();
            builder.UseJoliet = true;
            builder.VolumeIdentifier = $"WPILIB_{version.Replace('-', '_').Replace(' ','_').Replace('.', '_').ToUpper()}";

            foreach (var file in Directory.EnumerateFiles(input))
            {
                var fileName = Path.GetFileName(file);
                builder.AddFile(fileName, file);
            }

            builder.Build(output);

            return 0;
        }
    }
}
