using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using RePKG.Application.Package;
using RePKG.Application.Texture;
using RePKG.Application.Texture.Helpers;
using RePKG.Core.Package;
using RePKG.Core.Package.Interfaces;
using RePKG.Core.Texture;

namespace RePKG.Command
{
    public static class Repack
    {
        private static string T(string zh, string en) => Program.EnglishMode ? en : zh;

        private static readonly IPackageWriter _packageWriter;

        static Repack()
        {
            _packageWriter = new PackageWriter();
        }

        public static void Action(RepackOptions options)
        {
            Program.EnglishMode = options.English;

            var fileInfo = new FileInfo(options.Input);
            var dirInfo = new DirectoryInfo(options.Input);

            if (fileInfo.Exists)
            {
                PackTexFile(options, fileInfo);
            }
            else if (dirInfo.Exists)
            {
                PackDirectory(options, dirInfo);
            }
            else
            {
                Console.WriteLine(T("输入未找到", "Input not found"));
                Console.WriteLine(options.Input);
            }
        }

        private static void PackTexFile(RepackOptions options, FileInfo fileInfo)
        {
            var outputPath = options.Output;
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Path.ChangeExtension(fileInfo.FullName, ".tex");

            var format = TexFormat.RGBA8888;
            if (!string.IsNullOrEmpty(options.Format))
            {
                switch (options.Format.ToUpperInvariant())
                {
                    case "RGBA8888": format = TexFormat.RGBA8888; break;
                    case "R8": format = TexFormat.R8; break;
                    case "RG88": format = TexFormat.RG88; break;
                    case "MOBILE":
                    case "MOB":
                    case "RGB332": format = TexFormat.Mobile; break;
                    default:
                        Console.WriteLine(T($"不支持的格式: {options.Format}. 支持: RGBA8888, R8, RG88, MOBILE/MOB/RGB332",
                            $"Unsupported format: {options.Format}. Supported: RGBA8888, R8, RG88, MOBILE/MOB/RGB332"));
                        return;
                }
            }

            var isVideo = ImageToTexConverter.IsVideoFile(fileInfo.FullName);
            var isGif = fileInfo.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                && !isVideo && !options.NoGif;

            Console.WriteLine($"{T("正在转换 ", "Converting ")}{fileInfo.FullName} -> {outputPath}");

            Tex tex;
            if (isVideo)
            {
                Console.WriteLine(T("视频模式: MP4嵌入为视频纹理", "Video mode: MP4 embedded as video texture"));
                tex = ImageToTexConverter.ConvertFromVideo(
                    fileInfo.FullName, options.VideoWidth, options.VideoHeight, options.Lz4);
            }
            else if (isGif)
            {
                Console.WriteLine(T("GIF模式: 每帧打包为单独图片", "GIF mode: each frame packed as separate image"));
                tex = ImageToTexConverter.ConvertFromGif(fileInfo.FullName, options.Lz4);
            }
            else
            {
                tex = ImageToTexConverter.Convert(fileInfo.FullName, format, options.Lz4);
                Console.WriteLine($"{T("格式", "Format")}: {format}, LZ4: {options.Lz4}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

            using (var writer = new BinaryWriter(File.Open(outputPath, FileMode.Create, FileAccess.Write)))
            {
                var texWriter = TexWriter.Default;
                texWriter.WriteTo(writer, tex);
            }

            Console.WriteLine(T("完成", "Done"));
        }

        private static void PackDirectory(RepackOptions options, DirectoryInfo inputInfo)
        {
            var outputPath = options.Output;
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Path.Combine(Directory.GetCurrentDirectory(),
                    options.Mpkg ? "output.mpkg" : "output.pkg");

            var files = inputInfo.EnumerateFiles("*", SearchOption.AllDirectories);

            // Auto-detect magic: .mpkg -> PKGM0019 (Android), .pkg -> PKGV0005 (desktop)
            var magic = options.Magic;
            if (string.IsNullOrEmpty(magic))
            {
                if (options.Mpkg || (outputPath.EndsWith(".mpkg", StringComparison.OrdinalIgnoreCase)))
                    magic = "PKGM0019";
                else
                    magic = "PKGV0005";
            }

            var package = new Package { Magic = magic };

            var basePath = inputInfo.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var file in files)
            {
                var relativePath = file.FullName.Substring(basePath.Length + 1);
                var bytes = File.ReadAllBytes(file.FullName);

                package.Entries.Add(new PackageEntry
                {
                    FullPath = relativePath,
                    Bytes = bytes,
                    Type = PackageEntryTypeGetter.GetFromFileName(relativePath)
                });
            }

            if (package.Entries.Count == 0)
            {
                Console.WriteLine(T("输入目录中未找到文件", "No files found in input directory"));
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

            using (var writer = new BinaryWriter(File.Open(outputPath, FileMode.Create, FileAccess.Write)))
            {
                _packageWriter.WriteTo(writer, package);
            }

            Console.WriteLine($"{T("已创建包: ", "Package created: ")}{outputPath}");
            Console.WriteLine($"{T("条目数: ", "Entries: ")}{package.Entries.Count}");
            Console.WriteLine($"{T("魔术字: ", "Magic: ")}{package.Magic}");
        }
    }

    [Verb("repack", HelpText = "将目录打包为PKG/MPKG,或转换图片/视频/纹理为TEX格式")]
    public class RepackOptions
    {
        [Option('o', "output", Required = false, HelpText = "输出路径 (.tex为纹理文件, .pkg/.mpkg为包文件)")]
        public string Output { get; set; }

        [Option('m', "magic", Required = false, HelpText = "PKG头部魔术字: PKGV0005(桌面版,.pkg默认) / PKGM0019(Android版,.mpkg默认)")]
        public string Magic { get; set; }

        [Option('M', "mpkg", Required = false, HelpText = "创建Android MPKG包 (魔术字PKGM0019)")]
        public bool Mpkg { get; set; }

        [Option('f', "format", Required = false, HelpText = "输出纹理像素格式: RGBA8888, R8, RG88 (仅文件模式). 注意: 指定错误格式可能导致解码异常")]
        public string Format { get; set; }

        [Option("lz4", Required = false, HelpText = "为mipmap数据启用LZ4压缩以减小体积 (仅文件模式, 默认禁用)")]
        public bool Lz4 { get; set; }

        [Option("no-gif", Required = false, HelpText = "将GIF视为单帧图像处理 (仅文件模式)")]
        public bool NoGif { get; set; }

        [Option("video-width", Required = false, HelpText = "视频纹理的宽度 (像素), 省略时自动通过ffprobe检测")]
        public int VideoWidth { get; set; }

        [Option("video-height", Required = false, HelpText = "视频纹理的高度 (像素), 省略时自动通过ffprobe检测")]
        public int VideoHeight { get; set; }

        [Option("en", Required = false, HelpText = "Display output in English")]
        public bool English { get; set; }

        [Value(0, Required = true, HelpText = "输入文件或目录路径", MetaName = "Input")]
        public string Input { get; set; }
    }
}
