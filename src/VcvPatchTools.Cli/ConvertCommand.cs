using VcvPatchTools.Bridge;
using VcvPatchTools.Core.Patch;

namespace VcvPatchTools.Cli;

/// <summary>vcvpatch convert: a patch from Cardinal to VCV Rack, or the other way round.</summary>
internal static class ConvertCommand
{
    public const string Usage = "vcvpatch convert <input.vcv> [output.vcv] [--to cardinal|rack] [--force] [--no-cable-colors]";

    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine($"Usage: {Usage}");
            return 1;
        }

        string inputPath = InputPath.Resolve(args[1]);
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        string? outputPath = null;
        PatchOrigin? explicitTarget = null;
        bool force = false;
        bool noCableColors = false;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--to" && i + 1 < args.Length)
            {
                explicitTarget = args[i + 1].ToLowerInvariant() switch
                {
                    "cardinal" => PatchOrigin.Cardinal,
                    "rack" => PatchOrigin.Rack,
                    _ => throw new ArgumentException($"--to must be \"cardinal\" or \"rack\", got \"{args[i + 1]}\"."),
                };
                i++;
            }
            else if (args[i] == "--force")
            {
                force = true;
            }
            else if (args[i] == "--no-cable-colors")
            {
                noCableColors = true;
            }
            else if (outputPath is null)
            {
                outputPath = InputPath.Resolve(args[i]);
            }
            else
            {
                Console.Error.WriteLine($"Unexpected argument: \"{args[i]}\"");
                return 1;
            }
        }

        PatchArchive patch = PatchArchive.Read(File.ReadAllBytes(inputPath));
        DetectionResult detection = PatchDetector.Detect(patch);

        PatchOrigin? target = explicitTarget ?? ConvertedName.OppositeOf(detection.Origin);
        if (target is null)
        {
            Console.Error.WriteLine("Cannot infer the conversion direction (no Cardinal- or Rack-specific module found): specify --to cardinal|rack.");
            return 1;
        }

        outputPath ??= OutputPathResolver.Resolve(inputPath, target.Value, force);

        Console.WriteLine($"Detected origin : {ConvertedName.Describe(detection.Origin)}");
        Console.WriteLine($"Converting to   : {ConvertedName.Describe(target.Value)}");

        ConversionResult result = PatchConverter.Convert(patch, target.Value, remapCableColors: !noCableColors);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllBytes(outputPath, patch.Write());

        Console.WriteLine($"Written: {outputPath}");

        if (result.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Warnings ({result.Warnings.Count}):");
            foreach (string w in result.Warnings)
            {
                Console.WriteLine($"  - {w}");
            }
        }

        return 0;
    }

    /// <summary>The "detect" part of vcvpatch info: file format and Rack/Cardinal origin, with the modules that tell.</summary>
    public static void WriteOrigin(PatchArchive patch, TextWriter output)
    {
        DetectionResult detection = PatchDetector.Detect(patch);
        output.WriteLine($"Format: {(patch.WasArchive ? "tar+zstd archive" : "raw JSON")}");
        output.WriteLine($"Origin: {ConvertedName.Describe(detection.Origin)}");
        WriteModules(output, $"Cardinal modules ({detection.CardinalOnlyModules.Count})", detection.CardinalOnlyModules);
        WriteModules(output, $"VCV Rack Core modules, never resaved by Cardinal ({detection.RackOnlyModules.Count})", detection.RackOnlyModules);
    }

    private static void WriteModules(TextWriter output, string heading, List<ModuleSlug> modules)
    {
        if (modules.Count == 0)
        {
            return;
        }
        output.WriteLine($"{heading}: {string.Join(", ", modules.Select(s => s.Model).Distinct().OrderBy(s => s, StringComparer.Ordinal))}");
    }
}