using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using RePKG.Application.Package;
using RePKG.Application.Texture;
using RePKG.Application.Texture.Helpers;
using RePKG.Core.Package;
using RePKG.Core.Package.Enums;
using RePKG.Core.Package.Interfaces;
using RePKG.Core.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RePKG.Command
{
    public static class Replace
    {
        private static string T(string zh, string en) => Program.EnglishMode ? en : zh;

        private static readonly IPackageReader _packageReader;
        private static readonly IPackageWriter _packageWriter;
        private static readonly ITexReader _texReader;
        private static readonly TexToImageConverter _texToImage;

        static Replace()
        {
            _packageReader = new PackageReader { ReadEntryBytes = true };
            _packageWriter = new PackageWriter();
            _texReader = TexReader.Default;
            _texToImage = new TexToImageConverter();
        }

        public static void Action(ReplaceOptions options)
        {
            Program.EnglishMode = options.English;
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
                Console.WriteLine(T($"不匹配: 包内路径(-t)有 {pkgPaths.Count} 个, 但替换文件(-f)有 {filePaths.Count} 个",
                    $"Mismatch: {pkgPaths.Count} inside paths (-t) but {filePaths.Count} files (-f)"));
                Console.WriteLine(T("用法: repkg replace <pkg> -t <包内路径> -f <替换文件> [-o <输出>]",
                    "Usage: repkg replace <pkg> -t <inside path> -f <file> [-o <output>]"));
                return;
            }

            Console.WriteLine($"{T("正在读取: ", "Reading: ")}{inputPath}");

            Package package;
            using (var reader = new BinaryReader(File.OpenRead(inputPath)))
            {
                package = _packageReader.ReadFrom(reader);
            }

            Console.WriteLine($"{T("条目数: ", "Entries: ")}{package.Entries.Count}, {T("魔术字: ", "Magic: ")}{package.Magic}");

            for (int i = 0; i < pkgPaths.Count; i++)
            {
                var pkgPath = pkgPaths[i].Replace('\\', '/');
                var filePath = filePaths[i];

                var entry = package.Entries.FirstOrDefault(e =>
                    e.FullPath.Equals(pkgPath, StringComparison.OrdinalIgnoreCase) ||
                    e.FullPath.Replace('\\', '/').Equals(pkgPath, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    Console.WriteLine(T("包中未找到条目: ", "Entry not found in package: ") + pkgPaths[i]);
                    Console.WriteLine(T("可用条目:", "Available entries:"));
                    foreach (var e in package.Entries)
                        Console.WriteLine($"  {e.FullPath}");
                    return;
                }

                if (!File.Exists(filePath))
                {
                    Console.WriteLine(T("替换文件未找到: ", "Replacement file not found: ") + filePath);
                    return;
                }

                byte[] newBytes;

                if (entry.Type == EntryType.Tex && ShouldConvert(filePath, options.ForceConvert))
                {
                    var useLz4 = !options.NoLz4;
                    if (options.ForceConvert && Path.GetExtension(filePath) == ".tex")
                    {
                        Console.WriteLine($"{T("正在重编码: ", "Re-encoding: ")}{filePath} -> TEX");
                        newBytes = ReencodeTexFile(filePath, useLz4);
                    }
                    else
                    {
                        Console.WriteLine($"{T("正在转换: ", "Converting: ")}{filePath} -> TEX");
                        newBytes = ConvertToTexBytes(filePath, options.VideoWidth, options.VideoHeight, useLz4);
                    }
                }
                else
                {
                    newBytes = File.ReadAllBytes(filePath);
                }

                entry.Bytes = newBytes;
                entry.Type = PackageEntryTypeGetter.GetFromFileName(entry.FullPath);
                entry.Length = newBytes.Length;

                Console.WriteLine($"{T("已替换: ", "Replaced: ")}{entry.FullPath} ({newBytes.Length} bytes)");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

            using (var writer = new BinaryWriter(File.Create(outputPath)))
            {
                _packageWriter.WriteTo(writer, package);
            }

            Console.WriteLine($"{T("已输出包: ", "Package written: ")}{outputPath}");
            Console.WriteLine($"{T("条目数: ", "Entries: ")}{package.Entries.Count}, {T("魔术字: ", "Magic: ")}{package.Magic}");
        }

        private static readonly HashSet<string> ImageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tga", ".tiff", ".tif", ".gif"
        };

        private static bool ShouldConvert(string path, bool forceConvert)
        {
            var ext = Path.GetExtension(path);
            if (forceConvert) return true;
            if (ext == ".tex") return false;
            if (ImageToTexConverter.IsVideoFile(path)) return true;
            return ImageExts.Contains(ext);
        }

        private static byte[] ReencodeTexFile(string filePath, bool useLz4)
        {
            // Read existing TEX
            ITex tex;
            using (var reader = new BinaryReader(File.OpenRead(filePath)))
            {
                tex = _texReader.ReadFrom(reader);
            }

            if (tex.IsVideoTexture)
            {
                // Video TEX: extract MP4 bytes, re-wrap in video TEX
                var mp4Bytes = tex.FirstImage.FirstMipmap.Bytes;
                var width = tex.Header.TextureWidth;
                var height = tex.Header.ImageHeight;
                return BuildVideoTexBytes(mp4Bytes, width, height);
            }

            // Image or GIF: convert to PNG, reload, re-encode
            var result = _texToImage.ConvertToImage(tex);

            if (result.Format == MipmapFormat.ImagePNG)
            {
                using (var ms = new MemoryStream(result.Bytes))
                using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(ms))
                {
                    var newTex = ImageToTexConverter.Convert(image, TexFormat.RGBA8888, useLz4);
                    using (var outMs = new MemoryStream())
                    using (var writer = new BinaryWriter(outMs))
                    {
                        TexWriter.Default.WriteTo(writer, newTex);
                        return outMs.ToArray();
                    }
                }
            }

            if (result.Format == MipmapFormat.ImageGIF)
            {
                // Save GIF to temp, re-convert
                var tmp = Path.GetTempFileName();
                try
                {
                    File.WriteAllBytes(tmp, result.Bytes);
                    var newTex = ImageToTexConverter.ConvertFromGif(tmp, useLz4);
                    using (var outMs = new MemoryStream())
                    using (var writer = new BinaryWriter(outMs))
                    {
                        TexWriter.Default.WriteTo(writer, newTex);
                        return outMs.ToArray();
                    }
                }
                finally
                {
                    File.Delete(tmp);
                }
            }

            // Fallback: raw copy
            return File.ReadAllBytes(filePath);
        }

        private static byte[] BuildVideoTexBytes(byte[] mp4Data, int width, int height)
        {
            var mipmap = new TexMipmap
            {
                Width = width,
                Height = height,
                Bytes = mp4Data,
                Format = MipmapFormat.VideoMp4,
                IsLZ4Compressed = false,
                DecompressedBytesCount = mp4Data.Length
            };

            var texImage = new TexImage();
            texImage.Mipmaps.Add(mipmap);

            var imageContainer = new TexImageContainer
            {
                Magic = "TEXB0004",
                ImageContainerVersion = TexImageContainerVersion.Version4,
                ImageFormat = FreeImageFormat.FIF_UNKNOWN
            };
            imageContainer.Images.Add(texImage);

            var header = new TexHeader
            {
                Format = TexFormat.RGBA8888,
                Flags = TexFlags.IsVideoTexture | TexFlags.ClampUVs,
                TextureWidth = width,
                TextureHeight = height,
                ImageWidth = width,
                ImageHeight = height,
                UnkInt0 = 0
            };

            var tex = new Tex
            {
                Magic1 = "TEXV0005",
                Magic2 = "TEXI0001",
                Header = header,
                ImagesContainer = imageContainer
            };

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                TexWriter.Default.WriteTo(writer, tex);
                return ms.ToArray();
            }
        }

        private static bool IsAutoConvertFile(string path)
        {
            var ext = Path.GetExtension(path);
            if (ext == ".tex") return false;
            if (ImageToTexConverter.IsVideoFile(path)) return true;
            return ImageExts.Contains(ext);
        }

        private static byte[] ConvertToTexBytes(string filePath, int videoWidth, int videoHeight, bool useLz4 = true)
        {
            var isVideo = ImageToTexConverter.IsVideoFile(filePath);
            var ext = Path.GetExtension(filePath);
            var isGif = ext.Equals(".gif", StringComparison.OrdinalIgnoreCase) && !isVideo;

            Tex tex;
            if (isVideo)
            {
                tex = ImageToTexConverter.ConvertFromVideo(filePath, videoWidth, videoHeight, false);
            }
            else if (isGif)
            {
                tex = ImageToTexConverter.ConvertFromGif(filePath, useLz4);
            }
            else
            {
                tex = ImageToTexConverter.Convert(filePath, TexFormat.RGBA8888, useLz4);
            }

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                TexWriter.Default.WriteTo(writer, tex);
                return ms.ToArray();
            }
        }
    }

    [Verb("replace", HelpText = "替换PKG/MPKG内部文件,无需完整解包/打包")]
    public class ReplaceOptions
    {
        [Option('o', "output", Required = false, HelpText = "输出PKG/MPKG路径 (默认: input.replaced.pkg)")]
        public string Output { get; set; }

        [Option('t', "target", Required = true, HelpText = "包内文件路径 (可多次指定, 与-f按顺序配对). 例: -t materials/hero.tex -f ./hero_new.png", Min = 1)]
        public IEnumerable<string> Replacements { get; set; }

        [Option('f', "file", Required = true, HelpText = "本地替换文件路径 (与-t按顺序配对)", Min = 1)]
        public IEnumerable<string> Files { get; set; }

        [Option('F', "force", Required = false, HelpText = "强制重编码: 即使替换.tex也重新编码")]
        public bool ForceConvert { get; set; }

        [Option("no-lz4", Required = false, HelpText = "禁用mipmap数据的LZ4压缩 (默认启用)")]
        public bool NoLz4 { get; set; }

        [Option("video-width", Required = false, HelpText = "视频纹理的宽度 (像素), 省略时自动检测")]
        public int VideoWidth { get; set; }

        [Option("video-height", Required = false, HelpText = "视频纹理的高度 (像素), 省略时自动检测")]
        public int VideoHeight { get; set; }

        [Option("en", Required = false, HelpText = "Display output in English")]
        public bool English { get; set; }

        [Value(0, Required = true, HelpText = "输入PKG/MPKG文件路径", MetaName = "Input")]
        public string Input { get; set; }
    }
}
