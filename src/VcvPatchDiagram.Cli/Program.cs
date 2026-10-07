using VcvPatchDiagram.Cli;
using VcvPatchDiagram.Core;
using VcvPatchDiagram.Core.Analysis;

return Run(args);

static int Run(string[] args)
{
    if (args.Length >= 2 && args[0] == "catalog" && args[1] == "build")
    {
        string? src = Option(args, "--src");
        if (src is null)
        {
            PrintUsage();
            return 1;
        }
        return CatalogCommand.Build(src, Option(args, "--out") ?? "catalog/ports.json", Option(args, "--report"));
    }

    if (args.Length >= 2 && args[0] == "inspect")
    {
        PatchAnalysis? analysis = Load(args[1]);
        return analysis is null ? 1 : InspectCommand.Run(analysis, Console.Out);
    }

    if (args.Length >= 2 && args[0] == "render")
    {
        return Render(args);
    }

    PrintUsage();
    return 1;
}

static int Render(string[] args)
{
    string path = InputPath.Resolve(args[1]);
    string? output = Option(args, "-o");
    string formatText = Option(args, "-f") ?? (output is not null ? Path.GetExtension(output) : "html");
    if (!PatchDiagram.TryParseFormat(formatText, out DiagramFormat format))
    {
        Console.Error.WriteLine($"Unknown format \"{formatText}\" (html, svg, dot, mmd, json)");
        return 1;
    }

    PatchAnalysis? analysis = Load(path);
    if (analysis is null)
    {
        return 1;
    }

    string title = Path.GetFileNameWithoutExtension(path);
    // --unfold all | <group,group…> ; default: the folded overview.
    string? unfoldOption = Option(args, "--unfold");
    IReadOnlySet<string>? unfolded = unfoldOption switch
    {
        null => null,
        "all" => PatchDiagram.GroupKeys(analysis),
        _ => unfoldOption.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(),
    };
    string content = PatchDiagram.Export(PatchDiagram.Layout(analysis, title, unfolded, showMonitors: args.Contains("--scopes"), functionNames: args.Contains("--functions"), portTabs: !args.Contains("--no-ports"), libraryLinks: !args.Contains("--no-links")), format);
    output ??= Path.ChangeExtension(Path.GetFileName(path), PatchDiagram.Extension(format));
    File.WriteAllText(output, content);
    Console.WriteLine($"{format} → {output}");
    return 0;
}

static PatchAnalysis? Load(string rawPath)
{
    string path = InputPath.Resolve(rawPath);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return null;
    }
    return PatchDiagram.Analyze(File.ReadAllBytes(path));
}

static string? Option(string[] args, string name)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  vcvdiagram inspect <patch.vcv>");
    Console.Error.WriteLine("  vcvdiagram render <patch.vcv> [-f html|svg|dot|mmd|json] [-o <output file>] [--unfold all|<group,...>] [--scopes]");
    Console.Error.WriteLine("    (default: folded overview; group keys are listed by \"inspect\")");
    Console.Error.WriteLine("  vcvdiagram catalog build --src <Cardinal/plugins> [--out catalog/ports.json] [--report catalog/scan-report.txt]");
}