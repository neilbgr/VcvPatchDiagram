using VcvPatchBridge;

int exitCode = Run(args);
return exitCode;

static int Run(string[] args)
{
    if (args.Length == 0)
    {
        PrintUsage();
        return 1;
    }

    switch (args[0])
    {
        case "detect":
            return RunDetect(args);
        case "convert":
            return RunConvert(args);
        default:
            Console.Error.WriteLine($"Unknown command: \"{args[0]}\"");
            PrintUsage();
            return 1;
    }
}

static int RunDetect(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: vcvpatchbridge detect <patch.vcv>");
        return 1;
    }

    string path = args[1];
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return 1;
    }

    PatchFile patch = PatchFile.Load(path);
    DetectionResult detection = PatchDetector.Detect(patch);

    Console.WriteLine($"File         : {path}");
    Console.WriteLine($"Format       : {(patch.WasArchive ? "tar+zstd archive" : "raw JSON")}");
    Console.WriteLine($"Origin       : {Describe(detection.Origin)}");

    if (detection.CardinalOnlyModules.Count > 0)
    {
        Console.WriteLine($"Cardinal modules found ({detection.CardinalOnlyModules.Count}):");
        foreach (string m in detection.CardinalOnlyModules.Select(s => s.Model).Distinct().OrderBy(s => s))
        {
            Console.WriteLine($"  - {m}");
        }
    }

    if (detection.RackOnlyModules.Count > 0)
    {
        Console.WriteLine($"VCV Rack (Core, never resaved by Cardinal) modules found ({detection.RackOnlyModules.Count}):");
        foreach (string m in detection.RackOnlyModules.Select(s => s.Model).Distinct().OrderBy(s => s))
        {
            Console.WriteLine($"  - {m}");
        }
    }

    return 0;
}

static int RunConvert(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: vcvpatchbridge convert <input.vcv> [output.vcv] [--to cardinal|rack] [--force] [--no-cable-colors]");
        return 1;
    }

    string inputPath = args[1];

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
            outputPath = args[i];
        }
        else
        {
            Console.Error.WriteLine($"Unexpected argument: \"{args[i]}\"");
            return 1;
        }
    }

    PatchFile patch = PatchFile.Load(inputPath);
    DetectionResult detection = PatchDetector.Detect(patch);

    PatchOrigin target;
    if (explicitTarget is not null)
    {
        target = explicitTarget.Value;
    }
    else if (detection.Origin == PatchOrigin.Cardinal)
    {
        target = PatchOrigin.Rack;
    }
    else if (detection.Origin == PatchOrigin.Rack)
    {
        target = PatchOrigin.Cardinal;
    }
    else
    {
        Console.Error.WriteLine("Cannot infer the conversion direction (no Cardinal- or Rack-specific module found): specify --to cardinal|rack.");
        return 1;
    }

    outputPath ??= OutputPathResolver.Resolve(inputPath, target, force);

    Console.WriteLine($"Detected origin : {Describe(detection.Origin)}");
    Console.WriteLine($"Converting to   : {Describe(target)}");

    ConversionResult result = PatchConverter.Convert(patch, target, remapCableColors: !noCableColors);
    patch.Save(outputPath);

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

static string Describe(PatchOrigin origin) => origin switch
{
    PatchOrigin.Cardinal => "Cardinal",
    PatchOrigin.Rack => "VCV Rack",
    PatchOrigin.Ambiguous => "Ambiguous (no divergent module detected)",
    _ => origin.ToString(),
};

static void PrintUsage() => Console.WriteLine("""
    VcvPatchBridge - detects/converts a .vcv patch between VCV Rack and Cardinal

    Usage:
      vcvpatchbridge detect <patch.vcv>
      vcvpatchbridge convert <input.vcv> [output.vcv] [--to cardinal|rack] [--force] [--no-cable-colors]
    """);