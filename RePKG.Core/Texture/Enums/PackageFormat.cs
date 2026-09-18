namespace RePKG.Core.Texture
{
    /// <summary>
    /// 包来源平台，决定 .tex TEXI 头中 format 字段的映射。
    /// V = 桌面版 PKG (PKGV)，M = Android MPKG (PKGM)。
    /// 两者 .tex 二进制格式相同，但 format 编号含义不同。
    /// </summary>
    public enum PackageFormat
    {
        /// <summary>桌面版 Wallpaper Engine (PKGV0004/PKGV0005)</summary>
        V = 0,

        /// <summary>Android 版 Wallpaper Engine (PKGM0016/PKGM0019)</summary>
        M = 1,
    }
}
