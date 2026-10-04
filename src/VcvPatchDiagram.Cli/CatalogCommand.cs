using VcvPatchDiagram.Core.Catalog;

namespace VcvPatchDiagram.Cli;

/// <summary>"catalog build": scans every plugin under a Cardinal "plugins" folder and writes catalog/ports.json.</summary>
internal static class CatalogCommand
{
    public static int Build(string pluginsDir, string outputPath, string? reportPath)
    {
        if (!Directory.Exists(pluginsDir))
        {
            Console.Error.WriteLine($"Plugins folder not found: {pluginsDir}");
            return 1;
        }

        Dictionary<string, ModuleInfo> all = new Dictionary<string, ModuleInfo>(StringComparer.Ordinal);
        List<string> problems = new List<string>();

        foreach (string pluginDir in Directory.GetDirectories(pluginsDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            string manifestPath = Path.Combine(pluginDir, "plugin.json");
            string sourceDir = Path.Combine(pluginDir, "src");
            if (!File.Exists(manifestPath) || !Directory.Exists(sourceDir))
            {
                continue;
            }

            List<SourceFile> sources = Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".cpp", StringComparison.Ordinal) || p.EndsWith(".hpp", StringComparison.Ordinal) || p.EndsWith(".h", StringComparison.Ordinal))
                .Select(p => new SourceFile(p, File.ReadAllText(p)))
                .ToList();

            try
            {
                ScanResult scan = SourceScanner.ScanPlugin(File.ReadAllText(manifestPath), sources);
                foreach ((string key, ModuleInfo info) in scan.Modules)
                {
                    all[key] = info;
                }
                problems.AddRange(scan.Problems);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException)
            {
                problems.Add($"{Path.GetFileName(pluginDir)}: unreadable plugin.json ({ex.Message})");
            }
        }

        File.WriteAllText(outputPath, new PortCatalog(all).ToJson());
        int resolved = all.Values.Count(m => m.Inputs.Count > 0 || m.Outputs.Count > 0);
        Console.WriteLine($"{all.Count} modules, {resolved} with ports resolved, {problems.Count} problems → {outputPath}");

        if (reportPath is not null)
        {
            File.WriteAllLines(reportPath, problems);
            Console.WriteLine($"Problems report → {reportPath}");
        }
        return 0;
    }
}