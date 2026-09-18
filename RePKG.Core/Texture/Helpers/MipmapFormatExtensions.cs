using System;

namespace RePKG.Core.Texture
{
    public static class MipmapFormatExtensions
    {
        /// <summary>
        /// 图片格式 (值 >= 1000)
        /// </summary>
        public static bool IsImage(this MipmapFormat format)
        {
            return (int) format >= 1000;
        }

        /// <summary>
        /// 原始未压缩像素格式 (无需解码器，直接读取像素数据)
        /// </summary>
        public static bool IsRawFormat(this MipmapFormat format)
        {
            switch (format)
            {
                case MipmapFormat.RGBA8888:
                case MipmapFormat.R8:
                case MipmapFormat.RG88:
                case MipmapFormat.RGB565:
                case MipmapFormat.RGB888:
                case MipmapFormat.RGBA4444:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 压缩格式 (需要解码器解压为 RGBA8888)
        /// </summary>
        public static bool IsCompressed(this MipmapFormat format)
        {
            switch (format)
            {
                case MipmapFormat.CompressedDXT5:
                case MipmapFormat.CompressedDXT3:
                case MipmapFormat.CompressedDXT1:
                case MipmapFormat.CompressedDXT1Alpha:
                case MipmapFormat.CompressedETC2:
                case MipmapFormat.CompressedBC7:
                case MipmapFormat.CompressedBC4:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 文件扩展名
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">格式无对应扩展名</exception>
        public static string GetFileExtension(this MipmapFormat format)
        {
            switch (format)
            {
                case MipmapFormat.ImageBMP:
                    return "bmp";
                case MipmapFormat.ImageICO:
                    return "ico";
                case MipmapFormat.ImageJPEG:
                    return "jpg";
                case MipmapFormat.ImageJNG:
                    return "jng";
                case MipmapFormat.ImageKOALA:
                    return "koa";
                case MipmapFormat.ImageLBM:
                case MipmapFormat.ImageIFF:
                    return "lbm";
                case MipmapFormat.ImageMNG:
                    return "mng";
                case MipmapFormat.ImagePBM:
                case MipmapFormat.ImagePBMRAW:
                    return "pbm";
                case MipmapFormat.ImagePCD:
                    return "pcd";
                case MipmapFormat.ImagePCX:
                    return "pcx";
                case MipmapFormat.ImagePGM:
                case MipmapFormat.ImagePGMRAW:
                    return "pgm";
                case MipmapFormat.ImagePNG:
                    return "png";
                case MipmapFormat.ImagePPM:
                case MipmapFormat.ImagePPMRAW:
                    return "ppm";
                case MipmapFormat.ImageRAS:
                    return "ras";
                case MipmapFormat.ImageTARGA:
                    return "tga";
                case MipmapFormat.ImageTIFF:
                    return "tif";
                case MipmapFormat.ImageWBMP:
                    return "wbmp";
                case MipmapFormat.ImagePSD:
                    return "psd";
                case MipmapFormat.ImageCUT:
                    return "cut";
                case MipmapFormat.ImageXBM:
                    return "xbm";
                case MipmapFormat.ImageXPM:
                    return "xpm";
                case MipmapFormat.ImageDDS:
                    return "dds";
                case MipmapFormat.ImageGIF:
                    return "gif";
                case MipmapFormat.ImageHDR:
                    return "hdr";
                case MipmapFormat.ImageFAXG3:
                    return "g3";
                case MipmapFormat.ImageSGI:
                    return "sgi";
                case MipmapFormat.ImageEXR:
                    return "exr";
                case MipmapFormat.ImageJ2K:
                    return "j2k";
                case MipmapFormat.ImageJP2:
                    return "jp2";
                case MipmapFormat.ImagePFM:
                    return "pfm";
                case MipmapFormat.ImagePICT:
                    return "pict";
                case MipmapFormat.ImageRAW:
                    return "raw";
                case MipmapFormat.VideoMp4:
                    return "mp4";
                case MipmapFormat.RGBA8888:
                case MipmapFormat.R8:
                case MipmapFormat.RG88:
                case MipmapFormat.RGB565:
                case MipmapFormat.RGB888:
                case MipmapFormat.RGBA4444:
                case MipmapFormat.CompressedBC7:
                case MipmapFormat.CompressedBC4:
                    return "png";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
    }
}
