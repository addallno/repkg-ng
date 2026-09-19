using System;
using System.Collections.Generic;
using System.IO;
using RePKG.Core.Package;
using RePKG.Core.Package.Interfaces;

namespace RePKG.Application.Package
{
    public class PackageReader : IPackageReader
    {
        public bool ReadEntryBytes { get; set; } = true;

        public Core.Package.Package ReadFrom(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            
            var packageStart = reader.BaseStream.Position;
            var package = new Core.Package.Package
            {
                Magic = reader.ReadStringI32Size(maxLength: 32)
            };

            ReadEntries(package.Entries, reader);

            var dataStart = reader.BaseStream.Position;
            package.HeaderSize = dataStart - packageStart;

            if (!ReadEntryBytes)
                return package;

            PopulateEntriesWithData(dataStart, package.Entries, reader);

            return package;
        }

        private static void ReadEntries(List<PackageEntry> list, BinaryReader reader)
        {
            var entryCount = reader.ReadInt32();

            for (var i = 1; i <= entryCount; i++)
            {
                var fullPath = reader.ReadStringI32Size(maxLength: 255);

                list.Add(new PackageEntry
                {
                    FullPath = fullPath,
                    Offset = reader.ReadInt32(),
                    Length = reader.ReadInt32(),
                    Type = PackageEntryTypeGetter.GetFromFileName(fullPath)
                });
            }
        }

        private static void PopulateEntriesWithData(long dataStart, List<PackageEntry> entries, BinaryReader reader)
        {
            foreach (var entry in entries)
            {
                reader.BaseStream.Seek(entry.Offset + dataStart, SeekOrigin.Begin);
                if (entry.Length > int.MaxValue)
                    throw new InvalidDataException($"Entry too large to read: {entry.FullPath} ({entry.Length} bytes)");
                entry.Bytes = reader.ReadBytes((int)entry.Length);
            }
        }
    }
}