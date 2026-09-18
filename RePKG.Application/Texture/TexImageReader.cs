using System;
using System.IO;
using RePKG.Application.Exceptions;
using RePKG.Core.Texture;

namespace RePKG.Application.Texture
{
    public class TexImageReader : ITexImageReader
    {
        protected readonly ITexMipmapDecompressor _texMipmapDecompressor;
        public bool ReadMipmapBytes { get; set; } = true;
        public bool DecompressMipmapBytes { get; set; } = true;

        public TexImageReader(ITexMipmapDecompressor texMipmapDecompressor)
        {
            _texMipmapDecompressor = texMipmapDecompressor;
        }

        public ITexImage ReadFrom(
            BinaryReader reader,
            ITexImageContainer container,
            TexFormat texFormat,
            PackageFormat packageFormat = PackageFormat.V)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (container == null) throw new ArgumentNullException(nameof(container));

            var mipmapCount = reader.ReadInt32();

            if (mipmapCount > Constants.MaximumMipmapCount)
                throw new UnsafeTexException(
                    $"Mipmap count exceeds limit: {mipmapCount}/{Constants.MaximumMipmapCount}");

            var readFunction = PickMipmapReader(container.ImageContainerVersion);

            // 使用 TexFormatMapper 根据 PackageFormat 选择正确的格式映射
            var rawFormat = (int)texFormat;
            var format = TexFormatMapper.Map(rawFormat, packageFormat);

            var image = new TexImage();

            for (var i = 0; i < mipmapCount; i++)
            {
                var mipmap = readFunction(reader);
                mipmap.Format = format;

                if (DecompressMipmapBytes)
                    _texMipmapDecompressor.DecompressMipmap(mipmap);

                image.Mipmaps.Add(mipmap);
            }

            return image;
        }

        private TexMipmap ReadMipmapV1(BinaryReader reader)
        {
            return new TexMipmap
            {
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32(),
                Bytes = ReadBytes(reader)
            };
        }

        private TexMipmap ReadMipmapV2And3(BinaryReader reader)
        {
            return new TexMipmap
            {
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32(),
                IsLZ4Compressed = reader.ReadInt32() == 1,
                DecompressedBytesCount = reader.ReadInt32(),
                Bytes = ReadBytes(reader)
            };
        }
        private TexMipmap ReadMipmapV4(BinaryReader reader)
        {
            var param1 = reader.ReadInt32();
            if(param1 != 1)
            {
                throw new UnsafeTexException($"ReadMipmapV4 unknown param1: {param1}");
            }
            var param2= reader.ReadInt32();
            if (param2 != 2)
            {
                throw new UnsafeTexException($"ReadMipmapV4 unknown param2: {param2}");
            }
            var conditionJson = reader.ReadNString();

            var param3 = reader.ReadInt32();
            if (param3 != 1)
            {
                throw new UnsafeTexException($"ReadMipmapV4 unknown param3: {param3}");
            }
            return new TexMipmap
            {
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32(),
                IsLZ4Compressed = reader.ReadInt32() == 1,
                DecompressedBytesCount = reader.ReadInt32(),
                Bytes = ReadBytes(reader)
            };
        }
        private byte[] ReadBytes(BinaryReader reader)
        {
            var byteCount = reader.ReadInt32();

            if (reader.BaseStream.Position + byteCount > reader.BaseStream.Length)
                throw new UnsafeTexException("Detected invalid mipmap byte count - exceeds stream length");

            if (byteCount > Constants.MaximumMipmapByteCount)
                throw new UnsafeTexException(
                    $"Mipmap byte count exceeds maximum size: {byteCount}/{Constants.MaximumMipmapByteCount}");

            if (!ReadMipmapBytes)
            {
                reader.BaseStream.Seek(byteCount, SeekOrigin.Current);
                return null;
            }

            var bytes = new byte[byteCount];
            var bytesRead = reader.Read(bytes, 0, byteCount);

            if (bytesRead != byteCount)
                throw new Exception("Failed to read bytes from stream while reading mipmap");

            return bytes;
        }

        private Func<BinaryReader, TexMipmap> PickMipmapReader(TexImageContainerVersion containerVersion)
        {
            switch (containerVersion)
            {
                case TexImageContainerVersion.Version1:
                    return ReadMipmapV1;

                case TexImageContainerVersion.Version2:
                case TexImageContainerVersion.Version3:
                    return ReadMipmapV2And3;

                case TexImageContainerVersion.Version4:
                    return ReadMipmapV4;
                default:
                    throw new InvalidOperationException(
                        $"Tex image container version: {containerVersion} is not supported!");
            }
        }
    }
}