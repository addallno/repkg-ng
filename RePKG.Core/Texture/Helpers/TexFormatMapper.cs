using System;

namespace RePKG.Core.Texture
{
    /// <summary>
    /// TEXI 头中 format 字段 → MipmapFormat 映射。
    /// 桌面 (V) 和 Android (M) 使用不同的编号体系。
    /// 来源: V = linux-wallpaperengine, M = 反编译 libscenejni.so
    /// </summary>
    public static class TexFormatMapper
    {
        /// <summary>
        /// 桌面 PKG (PKGV) 格式映射
        /// </summary>
        public static MipmapFormat MapV(int rawFormat)
        {
            switch (rawFormat)
            {
                case 0: return MipmapFormat.RGBA8888;
                case 4: return MipmapFormat.CompressedDXT5;
                case 5: return MipmapFormat.CompressedETC2;
                case 6: return MipmapFormat.CompressedDXT3;
                case 7: return MipmapFormat.CompressedDXT1;
                case 8: return MipmapFormat.RG88;
                case 9: return MipmapFormat.R8;
                default:
                    throw new NotSupportedException(
                        $"PKG format {rawFormat} is not supported");
            }
        }

        /// <summary>
        /// Android MPKG (PKGM) 格式映射
        /// 来源: 反编译 libscenejni.so → ReadTextureDataShared + GLTexture
        /// </summary>
        public static MipmapFormat MapM(int rawFormat)
        {
            switch (rawFormat)
            {
                case 0: return MipmapFormat.CompressedDXT1;
                case 1: return MipmapFormat.CompressedDXT1Alpha;
                case 2: return MipmapFormat.CompressedDXT5;
                case 3: return MipmapFormat.CompressedBC7;
                case 4: return MipmapFormat.CompressedBC4;
                case 5: return MipmapFormat.CompressedETC2;
                case 6: return MipmapFormat.RGBA8888;
                case 7: return MipmapFormat.RGB565;
                case 8: return MipmapFormat.RGBA4444;
                case 9: return MipmapFormat.R8; // 验证: 752×902=678304字节=1字节/像素
                case 10: return MipmapFormat.CompressedBC7; // 0xa = BC7
                default:
                    throw new NotSupportedException(
                        $"MPKG format {rawFormat} is not supported");
            }
        }

        /// <summary>
        /// 根据 PackageFormat 选择映射方法
        /// </summary>
        public static MipmapFormat Map(int rawFormat, PackageFormat pkg)
        {
            return pkg == PackageFormat.M ? MapM(rawFormat) : MapV(rawFormat);
        }
    }
}
