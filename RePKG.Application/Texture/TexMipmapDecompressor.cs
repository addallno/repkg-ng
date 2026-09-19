using System;
using K4os.Compression.LZ4;
using RePKG.Application.Exceptions;
using RePKG.Core.Texture;

namespace RePKG.Application.Texture
{
    public class TexMipmapDecompressor : ITexMipmapDecompressor
    {
        public void DecompressMipmap(ITexMipmap mipmap)
        {
            if (mipmap == null) throw new ArgumentNullException(nameof(mipmap));

            if (mipmap.IsLZ4Compressed)
            {
                mipmap.Bytes = Lz4Decompress(mipmap.Bytes, mipmap.DecompressedBytesCount);
                mipmap.IsLZ4Compressed = false;
            }

            if (mipmap.Format.IsImage())
                return;

            // 压缩格式和需要像素转换的原始格式都由 TextureDecoder 统一处理
            if (mipmap.Format.IsCompressed() || mipmap.Format != MipmapFormat.RGBA8888)
            {
                mipmap.Bytes = TextureDecoder.Decode(mipmap.Width, mipmap.Height,
                    mipmap.Bytes, mipmap.Format);
                mipmap.Format = MipmapFormat.RGBA8888;
            }
        }

        private byte[] Lz4Decompress(byte[] bytes, int knownLength)
        {
            if (knownLength < 0)
                throw new UnsafeTexException($"Invalid LZ4 decompressed size: {knownLength}");

            var buffer = new byte[knownLength];

            var actualLength = LZ4Codec.Decode(
                bytes, 0, bytes.Length,
                buffer, 0, buffer.Length);

            if (actualLength != knownLength)
                throw new UnsafeTexException($"LZ4 decompressed size mismatch: {actualLength} != {knownLength}");

            return buffer;
        }
    }
}