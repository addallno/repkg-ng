using System;
using System.IO;
using RePKG.Core.Texture;

namespace RePKG.Application.Texture
{
    public class TexHeaderReader : ITexHeaderReader
    {
        public ITexHeader ReadFrom(BinaryReader reader, PackageFormat packageFormat = PackageFormat.V)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var rawFormat = reader.ReadInt32();

            var header = new TexHeader
            {
                RawFormat = rawFormat,
                Format = (TexFormat) rawFormat,
                Flags = (TexFlags) reader.ReadInt32(),
                TextureWidth = reader.ReadInt32(),
                TextureHeight = reader.ReadInt32(),
                ImageWidth = reader.ReadInt32(),
                ImageHeight = reader.ReadInt32(),
                UnkInt0 = reader.ReadUInt32()
            };

            // 桌面格式做合法性校验；Android 格式编号不同，跳过枚举校验
            if (packageFormat == PackageFormat.V && !header.Format.IsValid())
                throw new NotSupportedException(
                    $"PKG format {rawFormat} is not a valid desktop texture format");

            return header;
        }
    }
}