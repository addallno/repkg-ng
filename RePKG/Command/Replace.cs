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

            var pkgPaths = options.Replacements?.ToList() ?? new List<string>();
            var filePaths = options.Files?.ToList() ?? new List<string>();

            if (pkgPaths.Count == 0 || pkgPaths.Count != filePaths.Count)
            {
                Console.WriteLine($"Mismatch: {pkgPaths.Count} inside paths (-r) but {filePaths.Count} files (-f)");
                Console.WriteLine("Usage: repkg replace <pkg> -r <inside path> -f <file> [-o <output>]");
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
            for (int i = 0; i < pkgPaths.Count; i++)
            {
                var pkgPath = pkgPaths[i].Replace('\\', '/');
                var filePath = filePaths[i];

                // Find matching entry
                var entry = package.Entries.FirstOrDefault(e =>
                    e.FullPath.Equals(pkgPath, StringComparison.OrdinalIgnoreCase) ||
                    e.FullPath.Replace('\\', '/').Equals(pkgPath, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    Console.WriteLine($"Entry not found in package: {pkgPaths[i]}");
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

        [Option('r', "replace", Required = true, HelpText = "Path inside the package (can be specified multiple times)", Min = 1)]
        public IEnumerable<string> Replacements { get; set; }

        [Option('f', "file", Required = true, HelpText = "Local file to replace with (paired with -r by index)", Min = 1)]
        public IEnumerable<string> Files { get; set; }

        [Value(0, Required = true, HelpText = "Input PKG/MPKG path", MetaName = "Input")]
        public string Input { get; set; }
    }
}
