using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RePKG.Application.Package;
using RePKG.Application.Texture;
using RePKG.Core.Package;
using RePKG.Core.Package.Enums;
using RePKG.Core.Package.Interfaces;
using RePKG.Core.Texture;

namespace RePKG.Command
{
    public class Info
    {
        private static InfoOptions _options;
        private static string[] _projectInfoToPrint;

        private static readonly IPackageReader _reader;
        private static readonly ITexReader _texReader;

        static Info()
        {
            _reader = new PackageReader {ReadEntryBytes = false};
            _texReader = TexReader.Default;
        }

        public static void Action(InfoOptions options)
        {
            _options = options;
            Program.EnglishMode = options.English;

            if (string.IsNullOrEmpty(_options.ProjectInfo))
                _projectInfoToPrint = null;
            else
                _projectInfoToPrint = _options.ProjectInfo.Split(',');

            var fileInfo = new FileInfo(options.Input);
            var directoryInfo = new DirectoryInfo(options.Input);

            if (!fileInfo.Exists)
            {
                if (directoryInfo.Exists)
                {
                    if (_options.TexDirectory)
                        InfoTexDirectory(directoryInfo);
                    else
                        InfoPkgDirectory(directoryInfo);

                    Console.WriteLine("Done");
                    return;
                }

                Console.WriteLine(T("输入文件不存在!", "Input file/directory doesn't exist!"));
                Console.WriteLine(options.Input);
                return;
            }

            InfoFile(fileInfo);
            Console.WriteLine("Done");
        }

        private static string T(string zh, string en) => Program.EnglishMode ? en : zh;

        private static void InfoPkgDirectory(DirectoryInfo directoryInfo)
        {
            var rootDirectoryLength = directoryInfo.FullName.Length;

            foreach (var directory in directoryInfo.EnumerateDirectories())
            {
                foreach (var file in directory.EnumerateFiles("*.pkg")
                    .Concat(directory.EnumerateFiles("*.mpkg")))
                {
                    InfoPkg(file, file.FullName.Substring(rootDirectoryLength));
                }
            }
        }

        private static void InfoTexDirectory(DirectoryInfo directoryInfo)
        {
            foreach (var file in directoryInfo.EnumerateFiles("*.tex"))
            {
                InfoTex(file);
            }
        }

        private static void InfoFile(FileInfo file)
        {
            if (file.Extension.Equals(".pkg", StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals(".mpkg", StringComparison.OrdinalIgnoreCase))
                InfoPkg(file, Path.GetFullPath(file.Name));
            else if (file.Extension.Equals(".tex", StringComparison.OrdinalIgnoreCase))
                InfoTex(file);
            else
                Console.WriteLine(T($"不支持的文件扩展名: {file.Extension}", $"Unrecognized file extension: {file.Extension}"));
        }

        private static void InfoPkg(FileInfo file, string name)
        {
            Package package;
            using (var reader = new BinaryReader(file.Open(FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                package = _reader.ReadFrom(reader);
            }

            var entries = package.Entries;
            var totalDataSize = entries.Sum(e => e.Length);
            var texCount = entries.Count(e => e.Type == EntryType.Tex);
            var binCount = entries.Count - texCount;

            Console.WriteLine($"\r\n### Package: {name}");
            PrintProjectJsonFields(file, package);
            Console.WriteLine($"  Magic:          {package.Magic}");
            Console.WriteLine(T($"  文件大小:       ", "  File size:      ") + FormatSize(file.Length));
            Console.WriteLine(T($"  条目数:         ", "  Entries:        ") + $"{entries.Count} ({texCount} tex, {binCount} other)");
            Console.WriteLine(T($"  头部大小:       ", "  Header size:    ") + $"{package.HeaderSize} bytes");
            Console.WriteLine(T($"  数据大小:       ", "  Data size:      ") + FormatSize(totalDataSize));

            // Apply type filter for entry display
            var displayEntries = entries;
            if (_options.TexOnly)
                displayEntries = entries.Where(e => e.Type == EntryType.Tex).ToList();
            else if (_options.BinOnly)
                displayEntries = entries.Where(e => e.Type != EntryType.Tex).ToList();

            if (_options.PrintEntries || _options.AllMode)
            {
                Console.WriteLine();
                Console.WriteLine(T("  条目列表:", "  Entries:"));

                if (_options.Sort)
                {
                    if (_options.SortBy == "extension")
                        displayEntries.Sort((a, b) =>
                            String.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase));
                    else if (_options.SortBy == "size")
                        displayEntries.Sort((a, b) => a.Length.CompareTo(b.Length));
                    else
                        displayEntries.Sort((a, b) =>
                            String.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase));
                }

                foreach (var entry in displayEntries)
                {
                    var label = entry.Type == EntryType.Tex ? "TEX" : "BIN";
                    Console.WriteLine($"  * {entry.FullPath,-55} {label,5} {entry.Length,10} bytes  [0x{entry.Offset:X8}]");
                }

                if (_options.AllMode)
                {
                    Console.WriteLine();
                    Console.WriteLine(T($"    合计: {displayEntries.Count} 个条目, {FormatSize(displayEntries.Sum(e => e.Length))}",
                        $"    Total: {displayEntries.Count} entries, {FormatSize(displayEntries.Sum(e => e.Length))}"));
                }
            }
        }

        private static void PrintProjectJsonFields(FileInfo file, Package package)
        {
            JObject projectJson = null;

            var projectEntry = package.Entries.FirstOrDefault(e =>
                e.FullPath.Equals("project.json", StringComparison.OrdinalIgnoreCase));

            if (projectEntry != null)
            {
                using (var reader = new BinaryReader(file.Open(FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    reader.BaseStream.Seek(projectEntry.Offset + package.HeaderSize, SeekOrigin.Begin);
                    var bytes = reader.ReadBytes(projectEntry.Length);
                    if (bytes != null && bytes.Length > 0)
                    {
                        try
                        {
                            projectJson = JObject.Parse(Encoding.UTF8.GetString(bytes));
                        }
                        catch { }
                    }
                }
            }

            if (projectJson == null)
            {
                var directory = file.Directory;
                if (directory != null)
                {
                    var projectFiles = directory.GetFiles("project.json");
                    if (projectFiles.Length > 0 && projectFiles[0].Exists)
                    {
                        try
                        {
                            projectJson = JObject.Parse(File.ReadAllText(projectFiles[0].FullName));
                        }
                        catch { }
                    }
                }
            }

            if (projectJson == null)
                return;

            if (!MatchesFilter(projectJson))
                return;

            var title = projectJson.Value<string>("title");
            if (!string.IsNullOrEmpty(title))
                Console.WriteLine(T($"  标题:           ", "  Title:          ") + title);

            var workshopId = projectJson.Value<string>("workshopid");
            if (!string.IsNullOrEmpty(workshopId))
                Console.WriteLine(T($"  工坊 ID:        ", "  Workshop ID:    ") + $"{workshopId}  (WallpaperEngine appid: 431960)");

            var type = projectJson.Value<string>("type");
            if (!string.IsNullOrEmpty(type))
                Console.WriteLine(T($"  类型:           ", "  Type:           ") + type);

            var schema = projectJson.Value<string>("schema");
            if (!string.IsNullOrEmpty(schema))
                Console.WriteLine(T($"  模式:           ", "  Schema:         ") + schema);

            var tags = projectJson["tags"];
            if (tags is JArray tagArray && tagArray.Count > 0)
            {
                var tagStr = string.Join(", ", tagArray.Select(t => t.ToString()));
                Console.WriteLine(T($"  标签:           ", "  Tags:           ") + tagStr);
            }

            var description = projectJson.Value<string>("description");
            if (!string.IsNullOrEmpty(description))
            {
                var desc = description.Length > 120 ? description.Substring(0, 120) + "..." : description;
                Console.WriteLine(T($"  描述:           ", "  Description:    ") + desc);
            }

            var visible = projectJson.Value<string>("visible");
            if (!string.IsNullOrEmpty(visible))
                Console.WriteLine(T($"  可见性:         ", "  Visible:        ") + visible);

            var fileStr = projectJson.Value<string>("file");
            if (!string.IsNullOrEmpty(fileStr))
                Console.WriteLine(T($"  主文件:         ", "  Main file:      ") + fileStr);

            var preview = projectJson.Value<string>("preview");
            if (!string.IsNullOrEmpty(preview))
                Console.WriteLine(T($"  预览:           ", "  Preview:        ") + preview);

            var contentrating = projectJson.Value<string>("contentrating");
            if (!string.IsNullOrEmpty(contentrating))
                Console.WriteLine(T($"  内容分级:       ", "  Content rating: ") + contentrating);

            // In --all mode, also show any remaining project.json fields
            if (_options.AllMode)
            {
                var printedKeys = new HashSet<string> {
                    "title", "workshopid", "type", "schema", "tags",
                    "description", "visible", "file", "preview", "contentrating"
                };

                foreach (var prop in projectJson.Properties())
                {
                    if (printedKeys.Contains(prop.Name.ToLower()))
                        continue;
                    var val = projectJson[prop.Name];
                    if (val == null || val.Type == JTokenType.Null)
                        Console.WriteLine($"  {prop.Name}: null");
                    else
                        Console.WriteLine($"  {prop.Name}: {val}");
                }
            }

            if (_projectInfoToPrint?.Length > 0)
            {
                var allKeys = projectJson.Properties().Select(p => p.Name).ToList();
                IEnumerable<string> keysToPrint;
                if (_projectInfoToPrint.Length == 1 && _projectInfoToPrint[0] == "*")
                    keysToPrint = allKeys;
                else
                    keysToPrint = allKeys.Where(k =>
                        _projectInfoToPrint.Contains(k, StringComparer.OrdinalIgnoreCase));

                var printedKeys = new HashSet<string> {
                    "title", "workshopid", "type", "schema", "tags",
                    "description", "visible", "file", "preview", "contentrating"
                };

                foreach (var key in keysToPrint)
                {
                    if (printedKeys.Contains(key.ToLower()))
                        continue;
                    var val = projectJson[key];
                    if (val == null || val.Type == JTokenType.Null)
                        Console.WriteLine($"  {key}: null");
                    else
                        Console.WriteLine($"  {key}: {val}");
                }
            }
        }

        private static void InfoTex(FileInfo file)
        {
            ITex tex;
            using (var reader = new BinaryReader(file.Open(FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                tex = _texReader.ReadFrom(reader);
            }

            Console.WriteLine($"\r\n### Texture: {file.Name}");

            var header = tex.Header;
            var container = tex.ImagesContainer;
            var images = container?.Images;

            Console.WriteLine($"  Magic:          {tex.Magic1} / {tex.Magic2}");
            Console.WriteLine(T($"  格式:           ", "  Format:         ") + $"{header.Format} ({(int)header.Format})");
            Console.WriteLine(T($"  标志:           ", "  Flags:          ") + $"{FormatTexFlags(header.Flags)} ({(int)header.Flags})");
            Console.WriteLine(T($"  纹理尺寸:       ", "  Texture size:   ") + $"{header.TextureWidth} x {header.TextureHeight}");
            Console.WriteLine(T($"  图像尺寸:       ", "  Image size:     ") + $"{header.ImageWidth} x {header.ImageHeight}");
            Console.WriteLine(T($"  类型:           ", "  Type:           ") + $"{(tex.IsVideoTexture ? T("视频", "Video") : tex.IsGif ? "GIF" : T("静态", "Static"))}");

            if (container != null)
            {
                Console.WriteLine();
                Console.WriteLine(T("  图像容器:", "  Image container:"));
                Console.WriteLine($"    Magic:              {container.Magic}");
                Console.WriteLine(T($"    图像格式:           ", "    Image format:       ") + $"{container.ImageFormat} ({(int)container.ImageFormat})");
                Console.WriteLine(T($"    容器版本:           ", "    Container version:  ") + $"{(int)container.ImageContainerVersion} (V{container.ImageContainerVersion.ToString().Replace("Version", "")})");
                Console.WriteLine(T($"    图像数:             ", "    Images:             ") + $"{images?.Count ?? 0}");

                if (tex.FrameInfoContainer != null)
                {
                    var fic = tex.FrameInfoContainer;
                    Console.WriteLine(T($"    GIF 帧数:            ", "    GIF frames:         ") + $"{fic.Frames.Count}");
                    Console.WriteLine(T($"    GIF 尺寸:            ", "    GIF size:           ") + $"{fic.GifWidth} x {fic.GifHeight}");
                }

                if (images != null)
                {
                    var totalDataSize = 0L;
                    for (var i = 0; i < images.Count; i++)
                    {
                        var image = images[i];
                        var mipmaps = image.Mipmaps;
                        var first = image.FirstMipmap;
                        var imageDataSize = mipmaps.Sum(m => m.Bytes?.Length ?? 0);

                        Console.WriteLine();
                        Console.WriteLine(T($"  图像 #", "  Image #") + $"{i + 1}:");
                        Console.WriteLine(T($"    多级渐远纹理数:     ", "    Mipmaps:            ") + $"{mipmaps.Count}");

                        if (first != null)
                        {
                            Console.WriteLine(T($"    宽 x 高:            ", "    Width x Height:     ") + $"{first.Width} x {first.Height}");
                            Console.WriteLine(T($"    格式:               ", "    Format:             ") + $"{first.Format}");
                        }

                        if (mipmaps.Count > 0)
                        {
                            var compressedCount = mipmaps.Count(m => m.IsLZ4Compressed);
                            Console.WriteLine(T($"    LZ4 压缩:           ", "    LZ4 compressed:     ") + $"{compressedCount}/{mipmaps.Count}");
                            Console.WriteLine(T($"    数据大小:           ", "    Data size:          ") + FormatSize(imageDataSize));

                            if (mipmaps.Count > 1)
                            {
                                Console.WriteLine(T("    Mipmap 链:", "    Mipmap chain:"));
                                for (var j = 0; j < mipmaps.Count; j++)
                                {
                                    var m = mipmaps[j];
                                    var dataLen = m.Bytes?.Length ?? 0;
                                    var comp = m.IsLZ4Compressed ? " LZ4" : "    ";
                                    Console.WriteLine($"      [{j}] {m.Width,4} x {m.Height,-4} {comp} {FormatSize(dataLen)}");
                                }
                            }
                        }

                        totalDataSize += imageDataSize;
                    }

                    Console.WriteLine();
                    Console.WriteLine(T($"  纹理数据总计: ", "  Total texture data: ") + FormatSize(totalDataSize));
                }
            }
        }

        private static string FormatTexFlags(TexFlags flags)
        {
            if (flags == TexFlags.None)
                return "None";

            var parts = new List<string>();
            if (flags.HasFlag(TexFlags.NoInterpolation)) parts.Add("NoInterpolation");
            if (flags.HasFlag(TexFlags.ClampUVs)) parts.Add("ClampUVs");
            if (flags.HasFlag(TexFlags.IsGif)) parts.Add("IsGif");
            if (flags.HasFlag(TexFlags.IsVideoTexture)) parts.Add("IsVideoTexture");
            if (flags.HasFlag(TexFlags.Unk3)) parts.Add("Unk3");
            if (flags.HasFlag(TexFlags.Unk4)) parts.Add("Unk4");
            if (flags.HasFlag(TexFlags.Unk6)) parts.Add("Unk6");
            if (flags.HasFlag(TexFlags.Unk7)) parts.Add("Unk7");

            return parts.Count > 0 ? string.Join(" | ", parts) : flags.ToString();
        }

        private const long KB = 1024;
        private const long MB = 1024 * 1024;
        private const long GB = 1024 * 1024 * 1024;

        private static string FormatSize(long bytes)
        {
            if (bytes < KB)
                return Program.EnglishMode ? $"{bytes} bytes" : $"{bytes} 字节";
            if (bytes < MB)
                return Program.EnglishMode ? $"{bytes / (double)KB:F1} KB ({bytes} bytes)" : $"{bytes / (double)KB:F1} KB ({bytes} 字节)";
            if (bytes < GB)
                return $"{bytes / (double)MB:F2} MB ({bytes} bytes)";
            return $"{bytes / (double)GB:F2} GB ({bytes} bytes)";
        }

        private static bool MatchesFilter(JObject projectJson)
        {
            if (projectJson == null)
                return true;

            if (!string.IsNullOrEmpty(_options.TitleFilter))
            {
                var title = projectJson.Value<string>("title");
                if (!string.IsNullOrEmpty(title) && !title.Contains(_options.TitleFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }

    [Verb("info", HelpText = "查看 PKG/TEX 文件信息")]
    public class InfoOptions
    {
        [Value(0, Required = true, HelpText = "要查看信息的文件路径", MetaName = "Input file")]
        public string Input { get; set; }

        [Option('a', "all", Required = false, HelpText = "显示所有详细信息（完整 project.json + 条目列表）")]
        public bool AllMode { get; set; }

        [Option('e', "printentries", HelpText = "列出包内所有文件条目")]
        public bool PrintEntries { get; set; }

        [Option("tex-only", Required = false, HelpText = "仅显示纹理(TEX)条目（需配合 -e 使用）")]
        public bool TexOnly { get; set; }

        [Option("bin-only", Required = false, HelpText = "仅显示非纹理(BIN)条目（需配合 -e 使用）")]
        public bool BinOnly { get; set; }

        [Option('s', "sort", HelpText = "排序条目 (A-Z)", Default = false)]
        public bool Sort { get; set; }

        [Option('b', "sortby", HelpText = "排序依据: name, extension, size", Default = "name")]
        public string SortBy { get; set; }

        [Option('t', "tex", HelpText = "查看目录下所有 TEX 文件的信息")]
        public bool TexDirectory { get; set; }

        [Option('p', "projectinfo", HelpText = "显示 project.json 的指定字段 (逗号分隔, * 显示全部)")]
        public string ProjectInfo { get; set; }

        [Option("title-filter", HelpText = "按标题关键词过滤")]
        public string TitleFilter { get; set; }

        [Option("english", Required = false, HelpText = "Display output in English")]
        public bool English { get; set; }
    }
}
