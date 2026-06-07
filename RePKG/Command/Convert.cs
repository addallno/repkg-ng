using System;
using System.IO;
using System.Linq;
using CommandLine;
using RePKG.Application.Package;
using RePKG.Core.Package;
using RePKG.Core.Package.Interfaces;

namespace RePKG.Command
{
    public static class Convert
    {
        private static readonly IPackageReader _packageReader;
        private static readonly IPackageWriter _packageWriter;

        static Convert()
        {
            _packageReader = new PackageReader { ReadEntryBytes = false };
            _packageWriter = new PackageWriter();
        }

        public static void Action(ConvertOptions options)
        {
            var inputPath = options.Input;
            var outputPath = options.Output;

            if (string.IsNullOrEmpty(outputPath))
            {
                var ext = Path.GetExtension(inputPath);
                var name = Path.GetFileNameWithoutExtension(inputPath);
                var outExt = ext == ".mpkg" ? ".pkg" : ".mpkg";
                outputPath = Path.Combine(
                    Path.GetDirectoryName(inputPath) ?? ".",
                    $"{name}.converted{outExt}");
            }

            // Determine target magic
            var targetMagic = options.Magic;
            if (string.IsNullOrEmpty(targetMagic))
            {
                var inputExt = Path.GetExtension(inputPath)?.ToLowerInvariant();
                var outputExt = Path.GetExtension(outputPath)?.ToLowerInvariant();

                if (options.Android || outputExt == ".mpkg")
                    targetMagic = "PKGM0019";
                else if (options.Desktop || outputExt == ".pkg")
                    targetMagic = "PKGV0005";
                else
                    targetMagic = inputExt == ".mpkg" ? "PKGV0005" : "PKGM0019";
            }

            Console.WriteLine($"读取: {inputPath}");
            Console.WriteLine($"目标魔术字: {targetMagic}");

            Package package;
            using (var reader = new BinaryReader(File.OpenRead(inputPath)))
            {
                package = _packageReader.ReadFrom(reader);
            }

            Console.WriteLine($"条目数: {package.Entries.Count}, 原魔术字: {package.Magic}");

            package.Magic = targetMagic;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

            using (var writer = new BinaryWriter(File.Create(outputPath)))
            {
                _packageWriter.WriteTo(writer, package);
            }

            Console.WriteLine($"已输出: {outputPath}");
            Console.WriteLine($"条目数: {package.Entries.Count}, 魔术字: {targetMagic}");
        }
    }

    [Verb("convert", HelpText = "转换 PKG/MPKG 格式 (桌面 ←→ Android)")]
    public class ConvertOptions
    {
        [Option('o', "output", Required = false, HelpText = "输出路径 (默认: input.converted.pkg/.mpkg)")]
        public string Output { get; set; }

        [Option('m', "magic", Required = false, HelpText = "强制指定魔术字 (PKGV0005 / PKGM0019)")]
        public string Magic { get; set; }

        [Option("android", Required = false, HelpText = "转换为 Android MPKG 格式 (PKGM0019)")]
        public bool Android { get; set; }

        [Option("desktop", Required = false, HelpText = "转换为桌面 PKG 格式 (PKGV0005)")]
        public bool Desktop { get; set; }

        [Option("en", Required = false, HelpText = "Display output in English")]
        public bool English { get; set; }

        [Value(0, Required = true, HelpText = "输入 PKG/MPKG 文件路径", MetaName = "Input")]
        public string Input { get; set; }
    }
}
