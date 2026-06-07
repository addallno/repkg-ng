using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using RePKG.Application.Package;
using RePKG.Core.Package;
using RePKG.Core.Package.Interfaces;

namespace RePKG.Command
{
    public static class Replace
    {
        private static readonly IPackageReader _packageReader;
        private static readonly IPackageWriter _packageWriter;

        static Replace()
        {
            _packageReader = new PackageReader { ReadEntryBytes = true };
            _packageWriter = new PackageWriter();
        }

        public static void Action(ReplaceOptions options)
        {
            var inputPath = options.Input;
            var outputPath = options.Output;

            if (string.IsNullOrEmpty(outputPath))
            {
                var ext = Path.GetExtension(inputPath);
                var name = Path.GetFileNameWithoutExtension(inputPath);
                outputPath = Path.Combine(
                    Path.GetDirectoryName(inputPath) ?? ".",
                    $"{name}.replaced{ext}");
            }

            // Parse replacements
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in options.Replacements)
            {
                var idx = r.IndexOf('=');
                if (idx <= 0 || idx >= r.Length - 1)
                {
                    Console.WriteLine($"Invalid --replace format: {r} (expected path=filepath)");
                    return;
                }
                var pkgPath = r.Substring(0, idx).Trim();
                var filePath = r.Substring(idx + 1).Trim();
                replacements[pkgPath] = filePath;
            }

            if (replacements.Count == 0)
            {
                Console.WriteLine("No replacements specified. Use --replace path=filepath");
                return;
            }

            // Read input package
            Console.WriteLine($"Reading: {inputPath}");

            Package package;
            using (var reader = new BinaryReader(File.OpenRead(inputPath)))
            {
                package = _packageReader.ReadFrom(reader);
            }

            Console.WriteLine($"Entries: {package.Entries.Count}, Magic: {package.Magic}");

            // Apply replacements
            foreach (var kvp in replacements)
            {
                var pkgPath = kvp.Key.Replace('\\', '/');
                var filePath = kvp.Value;

                // Find matching entry
                var entry = package.Entries.FirstOrDefault(e =>
                    e.FullPath.Equals(pkgPath, StringComparison.OrdinalIgnoreCase) ||
                    e.FullPath.Replace('\\', '/').Equals(pkgPath, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    Console.WriteLine($"Entry not found in package: {kvp.Key}");
                    Console.WriteLine("Available entries:");
                    foreach (var e in package.Entries)
                        Console.WriteLine($"  {e.FullPath}");
                    return;
                }

                // Read replacement file
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Replacement file not found: {filePath}");
                    return;
                }

                var newBytes = File.ReadAllBytes(filePath);
                entry.Bytes = newBytes;
                entry.Type = PackageEntryTypeGetter.GetFromFileName(entry.FullPath);

                Console.WriteLine($"Replaced: {entry.FullPath} ({newBytes.Length} bytes)");
            }

            // Write output package
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

            using (var writer = new BinaryWriter(File.Create(outputPath)))
            {
                _packageWriter.WriteTo(writer, package);
            }

            Console.WriteLine($"Package written: {outputPath}");
            Console.WriteLine($"Entries: {package.Entries.Count}, Magic: {package.Magic}");
        }
    }

    [Verb("replace", HelpText = "Replace files inside PKG/MPKG without full repack.")]
    public class ReplaceOptions
    {
        [Option('o', "output", Required = false, HelpText = "Output PKG/MPKG path (default: input.replaced.pkg)")]
        public string Output { get; set; }

        [Option('r', "replace", Required = true, HelpText = "Replace mapping: path=filepath (can be specified multiple times)", Min = 1)]
        public IEnumerable<string> Replacements { get; set; }

        [Value(0, Required = true, HelpText = "Input PKG/MPKG path", MetaName = "Input")]
        public string Input { get; set; }
    }
}
