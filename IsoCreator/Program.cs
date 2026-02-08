using System.CommandLine;
using System.Globalization;
using DiscUtils.Iso9660;

Option<FileInfo> InputOption = new(name: "--input");
Option<FileInfo> OutputOption = new(name: "--output");
Option<string> VersionOption = new(name: "--installerversion");

RootCommand rootCommand = new("ISO Creator for WPILib Installer")
{
    InputOption,
    OutputOption,
    VersionOption
};

rootCommand.SetAction(parseResult =>
{
    FileInfo inputFile = parseResult.GetRequiredValue(InputOption);
    FileInfo outputFile = parseResult.GetRequiredValue(OutputOption);
    string version = parseResult.GetRequiredValue(VersionOption);

    if (!Directory.Exists(inputFile.FullName) || (inputFile.Attributes & FileAttributes.Directory) == 0)
    {
        Console.WriteLine("Input must be a directory and must exist");
        return 1;
    }

    CDBuilder builder = new()
    {
        UseJoliet = true,
        VolumeIdentifier = $"WPILIB_{version.Replace('-', '_').Replace(' ', '_').Replace('.', '_').ToUpper(CultureInfo.InvariantCulture)}"
    };

    foreach (var file in Directory.EnumerateFiles(inputFile.FullName))
    {
        var fileName = Path.GetFileName(file);
        builder.AddFile(fileName, file);
    }

    builder.Build(outputFile.FullName);

    return 0;
});

ParseResult parseResult = rootCommand.Parse(args);
return parseResult.Invoke();
