using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json;
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

                Console.WriteLine("Input file/directory doesn't exist!");
                Console.WriteLine(options.Input);
                return;
            }

            InfoFile(fileInfo);
            Console.WriteLine("Done");
        }

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
                Console.WriteLine($"Unrecognized file extension: {file.Extension}");
        }

        private static void InfoPkg(FileInfo file, string name)
        {
            var projectInfo = GetProjectInfo(file);

            if (!MatchesFilter(projectInfo))
                return;

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
            Console.WriteLine($"  Magic:          {package.Magic}");
            Console.WriteLine($"  File size:      {FormatSize(file.Length)}");
            Console.WriteLine($"  Entries:        {entries.Count} ({texCount} tex, {binCount} other)");
            Console.WriteLine($"  Header size:    {package.HeaderSize} bytes");
            Console.WriteLine($"  Data size:      {FormatSize(totalDataSize)}");

            if (projectInfo != null && _projectInfoToPrint?.Length > 0)
            {
                IEnumerable<string> projectInfoEnumerator;

                if (_projectInfoToPrint.Length == 1 && _projectInfoToPrint[0] == "*")
                    projectInfoEnumerator = Helper.GetPropertyKeysForDynamic(projectInfo);
                else
                {
                    projectInfoEnumerator = Helper.GetPropertyKeysForDynamic(projectInfo);
                    projectInfoEnumerator = projectInfoEnumerator.Where(x =>
                        _projectInfoToPrint.Contains(x, StringComparer.OrdinalIgnoreCase));
                }

                foreach (var key in projectInfoEnumerator)
                {
                    if (projectInfo[key] == null)
                        Console.WriteLine($"  {key}: null");
                    else
                        Console.WriteLine($"  {key}: {projectInfo[key]}");
                }
            }

            if (_options.PrintEntries)
            {
                Console.WriteLine();
                Console.WriteLine("  Entries:");

                if (_options.Sort)
                {
                    if (_options.SortBy == "extension")
                        entries.Sort((a, b) =>
                            String.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase));
                    else if (_options.SortBy == "size")
                        entries.Sort((a, b) => a.Length.CompareTo(b.Length));
                    else
                        entries.Sort((a, b) =>
                            String.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase));
                }

                foreach (var entry in entries)
                {
                    var label = entry.Type == EntryType.Tex ? "TEX" : "BIN";
                    Console.WriteLine($"  * {entry.FullPath,-55} {label,5} {entry.Length,10} bytes  [0x{entry.Offset:X8}]");
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
            Console.WriteLine($"  Format:         {header.Format} ({(int)header.Format})");
            Console.WriteLine($"  Flags:          {FormatTexFlags(header.Flags)} ({(int)header.Flags})");
            Console.WriteLine($"  Texture size:   {header.TextureWidth} x {header.TextureHeight}");
            Console.WriteLine($"  Image size:     {header.ImageWidth} x {header.ImageHeight}");
            Console.WriteLine($"  Type:           {(tex.IsVideoTexture ? "Video" : tex.IsGif ? "GIF" : "Static")}");

            if (container != null)
            {
                Console.WriteLine();
                Console.WriteLine("  Image container:");
                Console.WriteLine($"    Magic:              {container.Magic}");
                Console.WriteLine($"    Image format:       {container.ImageFormat} ({(int)container.ImageFormat})");
                Console.WriteLine($"    Container version:  {(int)container.ImageContainerVersion} (V{container.ImageContainerVersion.ToString().Replace("Version", "")})");
                Console.WriteLine($"    Images:             {images?.Count ?? 0}");

                if (tex.FrameInfoContainer != null)
                {
                    var fic = tex.FrameInfoContainer;
                    Console.WriteLine($"    GIF frames:         {fic.Frames.Count}");
                    Console.WriteLine($"    GIF size:           {fic.GifWidth} x {fic.GifHeight}");
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
                        Console.WriteLine($"  Image #{i + 1}:");
                        Console.WriteLine($"    Mipmaps:            {mipmaps.Count}");

                        if (first != null)
                        {
                            Console.WriteLine($"    Width x Height:     {first.Width} x {first.Height}");
                            Console.WriteLine($"    Format:             {first.Format}");
                        }

                        if (mipmaps.Count > 0)
                        {
                            var compressedCount = mipmaps.Count(m => m.IsLZ4Compressed);
                            Console.WriteLine($"    LZ4 compressed:     {compressedCount}/{mipmaps.Count}");
                            Console.WriteLine($"    Data size:          {FormatSize(imageDataSize)}");

                            if (mipmaps.Count > 1)
                            {
                                Console.WriteLine("    Mipmap chain:");
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
                    Console.WriteLine($"  Total texture data: {FormatSize(totalDataSize)}");
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
                return $"{bytes} bytes";
            if (bytes < MB)
                return $"{bytes / (double)KB:F1} KB ({bytes} bytes)";
            if (bytes < GB)
                return $"{bytes / (double)MB:F2} MB ({bytes} bytes)";
            return $"{bytes / (double)GB:F2} GB ({bytes} bytes)";
        }

        private static dynamic GetProjectInfo(FileInfo packageFile)
        {
            var directory = packageFile.Directory;
            if (directory == null)
                return null;

            var projectJson = directory.GetFiles("project.json");
            if (projectJson.Length == 0 || !projectJson[0].Exists)
                return null;

            return JsonConvert.DeserializeObject(File.ReadAllText(projectJson[0].FullName));
        }

        private static bool MatchesFilter(dynamic project)
        {
            if (project == null)
                return true;

            if (!string.IsNullOrEmpty(_options.TitleFilter))
            {
                var title = (string) project.title;
                if (!title.Contains(_options.TitleFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }

    [Verb("info", HelpText = "Dumps PKG/TEX info.")]
    public class InfoOptions
    {
        [Value(0, Required = true, HelpText = "Path to file which you want to get info about", MetaName = "Input file")]
        public string Input { get; set; }

        [Option('s', "sort", HelpText = "Sort entries a-z", Default = false)]
        public bool Sort { get; set; }

        [Option('b', "sortby", HelpText = "Sort by ... (available options: name, extension, size)", Default = "name")]
        public string SortBy { get; set; }

        [Option('t', "tex", HelpText = "Dump info about all tex files from specified directory")]
        public bool TexDirectory { get; set; }

        [Option('p', "projectinfo", HelpText = "Keys to dump from project.json (delimit using comma) (* for all)")]
        public string ProjectInfo { get; set; }

        [Option('e', "printentries", HelpText = "Print entries in packages")]
        public bool PrintEntries { get; set; }

        [Option("title-filter", HelpText = "Title filter")]
        public string TitleFilter { get; set; }
    }
}