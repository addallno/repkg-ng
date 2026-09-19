using System;

namespace RePKG.Application.Texture
{
    /// <summary>
    /// 统一纹理解码器，将所有 MipmapFormat 解压/转换为 RGBA8888。
    /// 结构: 辅助函数 → 各格式解码 → 主调度
    /// </summary>
    public static class TextureDecoder
    {
        #region 辅助函数

        private static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

        /// <summary>RGB565 → RGB 各8位</summary>
        private static void Unpack565(ushort c, out byte r, out byte g, out byte b)
        {
            r = (byte)((c >> 11) & 0x1F);
            g = (byte)((c >> 5) & 0x3F);
            b = (byte)(c & 0x1F);
            r = (byte)((r << 3) | (r >> 2));
            g = (byte)((g << 2) | (g >> 4));
            b = (byte)((b << 3) | (b >> 2));
        }

        /// <summary>在输出 RGBA 缓冲区中写入一个像素</summary>
        private static void SetPixel(byte[] out8, int idx, byte r, byte g, byte b, byte a)
        {
            out8[idx] = r;
            out8[idx + 1] = g;
            out8[idx + 2] = b;
            out8[idx + 3] = a;
        }

        #endregion

        #region DXT 解码 (移植自 DXT.cs / LibSquish)

        private static void DecodeDxt1Block(byte[] src, int si, byte[] dst, int di, int stride)
        {
            var c0raw = (ushort)(src[si] | (src[si + 1] << 8));
            var c1raw = (ushort)(src[si + 2] | (src[si + 3] << 8));
            Unpack565(c0raw, out var r0, out var g0, out var b0);
            Unpack565(c1raw, out var r1, out var g1, out var b1);

            byte r2, g2, b2, r3, g3, b3;
            byte a3 = 255;
            if (c0raw > c1raw)
            {
                r2 = Clamp((2 * r0 + r1) / 3); g2 = Clamp((2 * g0 + g1) / 3); b2 = Clamp((2 * b0 + b1) / 3);
                r3 = Clamp((r0 + 2 * r1) / 3); g3 = Clamp((g0 + 2 * g1) / 3); b3 = Clamp((b0 + 2 * b1) / 3);
            }
            else
            {
                r2 = Clamp((r0 + r1) / 2); g2 = Clamp((g0 + g1) / 2); b2 = Clamp((b0 + b1) / 2);
                r3 = 0; g3 = 0; b3 = 0; a3 = 0;
            }

            var idx = si + 4;
            for (int y = 0; y < 4; y++, di += stride)
            {
                var bits = src[idx++];
                for (int x = 0; x < 4; x++)
                {
                    var code = (bits >> (2 * x)) & 3;
                    switch (code)
                    {
                        case 0: SetPixel(dst, di + x * 4, r0, g0, b0, 255); break;
                        case 1: SetPixel(dst, di + x * 4, r1, g1, b1, 255); break;
                        case 2: SetPixel(dst, di + x * 4, r2, g2, b2, 255); break;
                        case 3: SetPixel(dst, di + x * 4, r3, g3, b3, a3); break;
                    }
                }
            }
        }

        private static void DecodeDxt3Block(byte[] src, int si, byte[] dst, int di, int stride)
        {
            // 8 bytes color (DXT1)
            DecodeDxt1Block(src, si + 8, dst, di, stride);

            // 覆盖 alpha: 4 bits per pixel, 8 bytes = 16 pixels
            for (int y = 0; y < 4; y++)
            {
                var rowDi = di + y * stride;
                for (int x = 0; x < 4; x++)
                {
                    var ai = si + y * 4 + x / 2;
                    var a4 = (src[ai] >> ((x & 1) * 4)) & 0x0F;
                    dst[rowDi + x * 4 + 3] = (byte)(a4 | (a4 << 4));
                }
            }
        }

        private static void DecodeDxt5Block(byte[] src, int si, byte[] dst, int di, int stride)
        {
            // Alpha block
            var a0 = src[si];
            var a1 = src[si + 1];
            byte[] alphaCodes = new byte[8];
            alphaCodes[0] = a0;
            alphaCodes[1] = a1;
            if (a0 <= a1)
            {
                for (int i = 1; i < 5; i++)
                    alphaCodes[1 + i] = (byte)(((5 - i) * a0 + i * a1) / 5);
                alphaCodes[6] = 0;
                alphaCodes[7] = 255;
            }
            else
            {
                for (int i = 1; i < 7; i++)
                    alphaCodes[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
            }

            var bits = 0UL;
            for (int i = 0; i < 6; i++)
                bits |= (ulong)src[si + 2 + i] << (8 * i);

            for (int y = 0; y < 4; y++, di += stride)
                for (int x = 0; x < 4; x++)
                    dst[di + x * 4 + 3] = alphaCodes[(bits >> (3 * (4 * y + x))) & 7];

            // Color block (DXT1)
            di -= stride * 4;

            // 保存 alpha 通道（DecodeDxt1Block 会覆盖 alpha）
            Span<byte> savedAlpha = stackalloc byte[16];
            for (int py = 0; py < 4; py++)
            {
                var rowDi = di + py * stride;
                for (int px = 0; px < 4; px++)
                    savedAlpha[py * 4 + px] = dst[rowDi + px * 4 + 3];
            }

            DecodeDxt1Block(src, si + 8, dst, di, stride);

            // 恢复 alpha 通道
            for (int py = 0; py < 4; py++)
            {
                var rowDi = di + py * stride;
                for (int px = 0; px < 4; px++)
                    dst[rowDi + px * 4 + 3] = savedAlpha[py * 4 + px];
            }
        }

        #endregion

        #region ETC2 RGBA8 解码 (EAC alpha + ETC2 color, 16 bytes/block)

        /// <summary>
        /// EAC alpha 查找表 (来自 libETC2 / Khronos 参考)
        /// AlphaTable[tableIndex, lookupIndex] → 修饰符值
        /// </summary>
        private static readonly int[,] AlphaTable = {
            { -3, -6, -9, -15, 2, 5, 8, 14 },
            { -3, -7, -10, -13, 2, 6, 9, 12 },
            { -2, -5, -8, -13, 1, 4, 7, 12 },
            { -2, -4, -6, -13, 1, 3, 5, 12 },
            { -3, -6, -8, -12, 2, 5, 7, 11 },
            { -3, -7, -9, -11, 2, 6, 8, 10 },
            { -4, -7, -8, -11, 3, 6, 7, 10 },
            { -3, -5, -8, -11, 2, 4, 7, 10 },
            { -2, -6, -8, -10, 1, 5, 7, 9 },
            { -2, -5, -8, -10, 1, 4, 7, 9 },
            { -2, -4, -8, -10, 1, 3, 7, 9 },
            { -2, -5, -7, -10, 1, 4, 6, 9 },
            { -3, -4, -7, -10, 2, 3, 6, 9 },
            { -1, -2, -3, -10, 0, 1, 2, 9 },
            { -4, -6, -8, -9, 3, 5, 7, 8 },
            { -3, -5, -7, -9, 2, 4, 6, 8 },
        };

        /// <summary>
        /// ETC2 修饰符表 (8组 × 4值)，精确匹配 Khronos/etcpak g_table[8][4]
        /// 索引: ModTable[tableCode, modifierIndex]
        /// </summary>
        private static readonly int[,] ModTable = {
            {  2,   8,  -2,  -8 },
            {  5,  17,  -5, -17 },
            {  9,  29,  -9, -29 },
            { 13,  42, -13, -42 },
            { 18,  60, -18, -60 },
            { 24,  80, -24, -80 },
            { 33, 106, -33,-106 },
            { 47, 183, -47,-183 },
        };

        /// <summary>
        /// T/H 模式的修饰码表 (table59T58H)
        /// </summary>
        private static readonly uint[] ThModTable = { 3, 6, 11, 16, 23, 32, 41, 64 };

        private static int ExtendSign(int val, int bits) =>
            (val << (32 - bits)) >> (32 - bits);

        /// <summary>字节序翻转 (32位)</summary>
        private static uint BSwap32(uint v) =>
            ((v & 0xFF) << 24) | ((v & 0xFF00) << 8) | ((v >> 8) & 0xFF00) | ((v >> 24) & 0xFF);

        /// <summary>5bit → 8bit 扩展</summary>
        private static int Expand5(int v) => (v << 3) | (v >> 2);
        /// <summary>4bit → 8bit 扩展 (nibble × 17)</summary>
        private static int Expand4(int v) => (v << 4) | v;
        /// <summary>6bit → 8bit 扩展</summary>
        private static int Expand6(int v) => (v << 2) | (v >> 4);
        /// <summary>7bit → 8bit 扩展</summary>
        private static int Expand7(int v) => (v << 1) | (v >> 6);

        /// <summary>
        /// 位扩散：将16位值的每个位放到32位的偶数位位置 (bit n → bit 2n)
        /// 用于 ETC2 修饰符位的反交织
        /// </summary>
        private static uint SpreadBits(uint v)
        {
            v = (v | (v << 8)) & 0x00FF00FF;
            v = (v | (v << 4)) & 0x0F0F0F0F;
            v = (v | (v << 2)) & 0x33333333;
            v = (v | (v << 1)) & 0x55555555;
            return v;
        }

        /// <summary>
        /// 解码 EAC alpha 块 (8 bytes) → 4x4 alpha 值
        /// 块结构: byte0=baseCodeword, byte1=multiplier(高4位)+tableIndex(低4位), bytes2-7=48bit索引(3bit×16)
        /// 像素排列: row-major, pixel(x,y) → bits[3*(y*4+x)..3*(y*4+x)+2]
        /// </summary>
        private static void DecodeEacAlphaBlock(byte[] src, int si, byte[] dst, int di, int stride)
        {
            var baseCodeword = src[si];
            var multiplier = (src[si + 1] >> 4) & 0x0F;
            var tableIdx = src[si + 1] & 0x0F;

            // 读取48-bit索引 (3 bits × 16 pixels)
            ulong bits = 0;
            for (int i = 0; i < 6; i++)
                bits |= (ulong)src[si + 2 + i] << (8 * i);

            for (int py = 0; py < 4; py++)
            {
                var rowDi = di + py * stride;
                for (int px = 0; px < 4; px++)
                {
                    var pixelIdx = py * 4 + px;
                    var lookupIdx = (int)((bits >> (pixelIdx * 3)) & 7);

                    var alpha = baseCodeword + AlphaTable[tableIdx, lookupIdx] * multiplier;
                    dst[rowDi + px * 4 + 3] = Clamp(alpha);
                }
            }
        }

        /// <summary>
        /// 解码 ETC2 color 块 (8 bytes) → 4x4 RGB (alpha 保留不写)
        /// 精确翻译自 etcpak DecodeRGBPart / DecodeRGBAPart
        /// </summary>
        private static void DecodeEtc2ColorBlock(byte[] src, int si, byte[] dst, int di, int stride)
        {
            // 读取8字节颜色数据，bswap 每个32位字以匹配 etcpak 位布局
            uint lo = BSwap32((uint)(src[si] | (src[si + 1] << 8) | (src[si + 2] << 16) | (src[si + 3] << 24)));
            uint hi = BSwap32((uint)(src[si + 4] | (src[si + 5] << 8) | (src[si + 6] << 16) | (src[si + 7] << 24)));
            ulong d = (ulong)hi << 32 | lo;

            // table1 (bits 7-5) 用于子块2, table2 (bits 4-2) 用于子块1
            uint table1 = (uint)((d >> 5) & 7);
            uint table2 = (uint)((d >> 2) & 7);

            if ((d & 0x2) != 0)
            {
                // Differential mode (diff=1): 可能是普通差分/T/H/Planar
                int r0 = (int)((d >> 27) & 0x1F);
                int g0 = (int)((d >> 19) & 0x1F);
                int b0 = (int)((d >> 11) & 0x1F);

                // 符号扩展: 取 d 的低32位 (与 etcpak 的 int32_t(d) 等价)
                int d32 = (int)lo;
                int dr = (d32 << 5) >> 29;
                int dg = (d32 << 13) >> 29;
                int db = (d32 << 21) >> 29;

                int r1 = r0 + dr;
                int g1 = g0 + dg;
                int b1 = b0 + db;

                // 溢出检测 → T/H/Planar 模式
                if (r1 < 0 || r1 > 31)
                {
                    DecodeTMode(d, dst, di, stride);
                    return;
                }
                if (g1 < 0 || g1 > 31)
                {
                    DecodeHMode(d, dst, di, stride);
                    return;
                }
                if (b1 < 0 || b1 > 31)
                {
                    DecodePlanarMode(d, dst, di, stride);
                    return;
                }

                // 普通差分模式
                int[] cr = { Expand5(r0), Expand5(r1) };
                int[] cg = { Expand5(g0), Expand5(g1) };
                int[] cb = { Expand5(b0), Expand5(b1) };
                uint[] tcw = { table2, table1 };

                // 反交织修饰符位: modLsb=LSB, modMsb=MSB
                uint modLsb = SpreadBits((uint)((d >> 32) & 0xFFFF));
                uint modMsb = SpreadBits((uint)(d >> 48));
                uint idx = modLsb | (modMsb << 1);

                if ((d & 0x1) != 0)
                {
                    // flip=1: 水平分割, table 按行选 (row/2)
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 4; j++)
                        {
                            int mod = ModTable[tcw[j / 2], idx & 3];
                            idx >>= 2;
                            SetPixel(dst, di + j * stride + i * 4,
                                Clamp(cr[j / 2] + mod), Clamp(cg[j / 2] + mod), Clamp(cb[j / 2] + mod), 0);
                        }
                }
                else
                {
                    // flip=0: 垂直分割, table 按列选 (col/2)
                    for (int i = 0; i < 4; i++)
                    {
                        int t = (int)tcw[i / 2];
                        int r = cr[i / 2], g = cg[i / 2], b = cb[i / 2];
                        for (int j = 0; j < 4; j++)
                        {
                            int mod = ModTable[t, idx & 3];
                            idx >>= 2;
                            SetPixel(dst, di + j * stride + i * 4,
                                Clamp(r + mod), Clamp(g + mod), Clamp(b + mod), 0);
                        }
                    }
                }
            }
            else
            {
                // Individual mode (diff=0): 两个独立4bit基色
                int[] cr = { Expand4((int)((d >> 28) & 0xF)), Expand4((int)((d >> 24) & 0xF)) };
                int[] cg = { Expand4((int)((d >> 20) & 0xF)), Expand4((int)((d >> 16) & 0xF)) };
                int[] cb = { Expand4((int)((d >> 12) & 0xF)), Expand4((int)((d >> 8) & 0xF)) };
                uint[] tcw = { table2, table1 };

                uint modLsb2 = SpreadBits((uint)((d >> 32) & 0xFFFF));
                uint modMsb2 = SpreadBits((uint)(d >> 48));
                uint idx2 = modLsb2 | (modMsb2 << 1);

                if ((d & 0x1) != 0)
                {
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 4; j++)
                        {
                            int mod = ModTable[tcw[j / 2], idx2 & 3];
                            idx2 >>= 2;
                            SetPixel(dst, di + j * stride + i * 4,
                                Clamp(cr[j / 2] + mod), Clamp(cg[j / 2] + mod), Clamp(cb[j / 2] + mod), 0);
                        }
                }
                else
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int t = (int)tcw[i / 2];
                        int r = cr[i / 2], g = cg[i / 2], b = cb[i / 2];
                        for (int j = 0; j < 4; j++)
                        {
                            int mod = ModTable[t, idx2 & 3];
                            idx2 >>= 2;
                            SetPixel(dst, di + j * stride + i * 4,
                                Clamp(r + mod), Clamp(g + mod), Clamp(b + mod), 0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// T 模式解码: 当 diff=1 且 R1+dR 溢出时触发
        /// R 基色拆分为高2位(rh) + 低2位(rl)，重建为8位
        /// </summary>
        private static void DecodeTMode(ulong d, byte[] dst, int di, int stride)
        {
            int r0 = (int)((d >> 24) & 0x1B);
            int rh0 = (r0 >> 3) & 0x3;
            int rl0 = r0 & 0x3;
            int g0 = (int)((d >> 20) & 0xF);
            int b0 = (int)((d >> 16) & 0xF);

            int r1 = (int)((d >> 12) & 0xF);
            int g1 = (int)((d >> 8) & 0xF);
            int b1 = (int)((d >> 4) & 0xF);

            int cr0 = (rh0 << 6) | (rl0 << 4) | (rh0 << 2) | rl0;
            int cg0 = Expand4(g0);
            int cb0 = Expand4(b0);
            int cr1 = Expand4(r1);
            int cg1 = Expand4(g1);
            int cb1 = Expand4(b1);

            int codewordHi = (int)((d >> 2) & 0x3);
            int codewordLo = (int)(d & 0x1);
            int codeword = (codewordHi << 1) | codewordLo;
            uint modVal = ThModTable[codeword];

            int c2r = Clamp(cr1 + (int)modVal);
            int c2g = Clamp(cg1 + (int)modVal);
            int c2b = Clamp(cb1 + (int)modVal);
            int c3r = Clamp(cr1 - (int)modVal);
            int c3g = Clamp(cg1 - (int)modVal);
            int c3b = Clamp(cb1 - (int)modVal);

            uint indexes = (uint)(d >> 32);
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    int index = (int)((((indexes >> (j + i * 4 + 16)) & 1) << 1)
                              | ((indexes >> (j + i * 4)) & 1));

                    int r, g, b;
                    switch (index)
                    {
                        case 0: r = cr0; g = cg0; b = cb0; break;
                        case 1: r = c2r; g = c2g; b = c2b; break;
                        case 2: r = cr1; g = cg1; b = cb1; break;
                        default: r = c3r; g = c3g; b = c3b; break;
                    }
                    SetPixel(dst, di + j * stride + i * 4, (byte)r, (byte)g, (byte)b, 0);
                }
            }
        }

        /// <summary>
        /// H 模式解码: 当 diff=1 且 G1+dG 溢出时触发
        /// 两个444颜色 + 距离表
        /// </summary>
        private static void DecodeHMode(ulong d, byte[] dst, int di, int stride)
        {
            int r0 = (int)(((d >> 27) & 0x8) | ((d >> 24) & 0x7));
            int g0 = (int)(((d >> 23) & 0xE) | ((d >> 20) & 0x1));
            int b0 = (int)(((d >> 19) & 0xF));

            int r1 = (int)((d >> 15) & 0xF);
            int g1 = (int)((d >> 11) & 0xF);
            int b1 = (int)((d >> 7) & 0xF);

            int cr0 = Expand4(r0);
            int cg0 = Expand4(g0);
            int cb0 = Expand4(b0);
            int cr1 = Expand4(r1);
            int cg1 = Expand4(g1);
            int cb1 = Expand4(b1);

            uint c0val = (uint)((cr0 << 16) + (cg0 << 8) + cb0);
            uint c1val = (uint)((cr1 << 16) + (cg1 << 8) + cb1);
            int codewordHi = (int)((d >> 2) & 0x3);
            int codewordLo;
            if (c0val >= c1val)
                codewordLo = 1;
            else
                codewordLo = 0;
            int codeword = (codewordHi << 1) | codewordLo;
            uint modVal = ThModTable[codeword];

            int c0r = Clamp(cr0 + (int)modVal);
            int c0g = Clamp(cg0 + (int)modVal);
            int c0b = Clamp(cb0 + (int)modVal);
            int c1r = Clamp(cr0 - (int)modVal);
            int c1g = Clamp(cg0 - (int)modVal);
            int c1b = Clamp(cb0 - (int)modVal);
            int c2r = Clamp(cr1 + (int)modVal);
            int c2g = Clamp(cg1 + (int)modVal);
            int c2b = Clamp(cb1 + (int)modVal);
            int c3r = Clamp(cr1 - (int)modVal);
            int c3g = Clamp(cg1 - (int)modVal);
            int c3b = Clamp(cb1 - (int)modVal);

            uint indexes = (uint)(d >> 32);
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    int index = (int)((((indexes >> (j + i * 4 + 16)) & 1) << 1)
                              | ((indexes >> (j + i * 4)) & 1));

                    int r, g, b;
                    switch (index)
                    {
                        case 0: r = c0r; g = c0g; b = c0b; break;
                        case 1: r = c1r; g = c1g; b = c1b; break;
                        case 2: r = c2r; g = c2g; b = c2b; break;
                        default: r = c3r; g = c3g; b = c3b; break;
                    }
                    SetPixel(dst, di + j * stride + i * 4, (byte)r, (byte)g, (byte)b, 0);
                }
            }
        }

        /// <summary>
        /// Planar 模式解码: 当 diff=1 且 B1+dB 溢出时触发
        /// 三个控制点颜色 O/H/V，每像素双线性插值
        /// </summary>
        private static void DecodePlanarMode(ulong d, byte[] dst, int di, int stride)
        {
            // V, H 颜色从高32位提取 (block >> 32+)
            int bv = Expand6((int)((d >> 32) & 0x3F));
            int gv = Expand7((int)((d >> 38) & 0x7F));
            int rv = Expand6((int)((d >> 45) & 0x3F));

            int bh = Expand6((int)((d >> 51) & 0x3F));
            int gh = Expand7((int)((d >> 57) & 0x7F));

            // O 颜色和 rh 从低32位提取 (block >> 0~25)
            int rh0 = (int)((d >> 0) & 0x1);
            int rh1 = (int)(((d >> 2) & 0x1F) << 1);
            int rh = Expand6(rh0 | rh1);

            int bo0 = (int)((d >> 7) & 0x7);
            int bo1 = (int)(((d >> 11) & 0x3) << 3);
            int bo2 = (int)(((d >> 16) & 0x1) << 5);
            int bo = Expand6(bo0 | bo1 | bo2);
            int go0 = (int)((d >> 17) & 0x3F);
            int go1 = (int)(((d >> 24) & 0x1) << 6);
            int go = Expand7(go0 | go1);
            int ro = Expand6((int)((d >> 25) & 0x3F));

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    int r = Clamp((i * (rh - ro) + j * (rv - ro) + 4 * ro + 2) >> 2);
                    int g = Clamp((i * (gh - go) + j * (gv - go) + 4 * go + 2) >> 2);
                    int b = Clamp((i * (bh - bo) + j * (bv - bo) + 4 * bo + 2) >> 2);
                    SetPixel(dst, di + j * stride + i * 4, (byte)r, (byte)g, (byte)b, 0);
                }
            }
        }

        /// <summary>
        /// 解码 ETC2_RGBA8 块 (16 bytes): 前8字节EAC alpha + 后8字节ETC2 color
        /// 来源: 反编译 libscenejni.so → GL_COMPRESSED_RGBA8_ETC2_EAC (0x9278)
        /// </summary>
        private static void DecodeEtc2A8Block(byte[] src, int si, byte[] dst, int di, int stride)
        {
            DecodeEacAlphaBlock(src, si, dst, di, stride);

            // 保存 alpha 通道（DecodeEtc2ColorBlock 会将其清零）
            Span<byte> savedAlpha = stackalloc byte[16];
            for (int py = 0; py < 4; py++)
            {
                var rowDi = di + py * stride;
                for (int px = 0; px < 4; px++)
                    savedAlpha[py * 4 + px] = dst[rowDi + px * 4 + 3];
            }

            DecodeEtc2ColorBlock(src, si + 8, dst, di, stride);

            // 恢复 alpha 通道
            for (int py = 0; py < 4; py++)
            {
                var rowDi = di + py * stride;
                for (int px = 0; px < 4; px++)
                    dst[rowDi + px * 4 + 3] = savedAlpha[py * 4 + px];
            }
        }

        /// <summary>
        /// 解码纯 ETC2 color 块 (8 bytes, 无 alpha)
        /// </summary>
        private static void DecodeEtc2Block(byte[] src, int si, byte[] dst, int di, int stride)
        {
            DecodeEtc2ColorBlock(src, si, dst, di, stride);
            // alpha 全部设为255
            for (int py = 0; py < 4; py++)
            {
                var rowDi = di + py * stride;
                for (int px = 0; px < 4; px++)
                    dst[rowDi + px * 4 + 3] = 255;
            }
        }

        #endregion

        #region BC4 解码 (单通道, 8 bytes/block)

        /// <summary>
        /// 解码 BC4 块 (8 bytes): 结构与 EAC alpha 完全相同，但输出为灰度 (R=G=B=alpha值, A=255)
        /// BC4 用于单通道数据（如高度图、遮罩），只存储红色通道
        /// </summary>
        private static void DecodeBc4Block(byte[] src, int si, byte[] dst, int di, int stride)
        {
            var baseCodeword = src[si];
            var multiplier = (src[si + 1] >> 4) & 0x0F;
            var tableIdx = src[si + 1] & 0x0F;

            ulong bits = 0;
            for (int i = 0; i < 6; i++)
                bits |= (ulong)src[si + 2 + i] << (8 * i);

            for (int py = 0; py < 4; py++)
            {
                var rowDi = di + py * stride;
                for (int px = 0; px < 4; px++)
                {
                    var pixelIdx = py * 4 + px;
                    var lookupIdx = (int)((bits >> (pixelIdx * 3)) & 7);
                    var val = Clamp(baseCodeword + AlphaTable[tableIdx, lookupIdx] * multiplier);

                    // BC4 输出: R=G=B=val, A=255 (灰度)
                    dst[rowDi + px * 4] = val;
                    dst[rowDi + px * 4 + 1] = val;
                    dst[rowDi + px * 4 + 2] = val;
                    dst[rowDi + px * 4 + 3] = 255;
                }
            }
        }

        #endregion

        #region BC7 解码 (16 bytes/block, 8种模式)

        private static readonly int[] Bc7Anchor2 = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
        };

        private static readonly int[] Bc7Weight2 = { 0, 9, 3, 6 };
        private static readonly int[] Bc7Weight3 = { 0, 4, 9, 13, 17, 21, 26, 31 };

        private struct Bc7ModeInfo
        {
            public int ColorBits, AlphaBits, ColorIndexBits, AlphaIndexBits, NumPartitions;
            public bool HasRotation;
        }

        private static readonly Bc7ModeInfo[] Bc7Modes = {
            new Bc7ModeInfo { ColorBits = 4, AlphaBits = 0, ColorIndexBits = 3, AlphaIndexBits = 0, NumPartitions = 0, HasRotation = false },
            new Bc7ModeInfo { ColorBits = 6, AlphaBits = 0, ColorIndexBits = 3, AlphaIndexBits = 0, NumPartitions = 6, HasRotation = false },
            new Bc7ModeInfo { ColorBits = 5, AlphaBits = 0, ColorIndexBits = 2, AlphaIndexBits = 0, NumPartitions = 6, HasRotation = false },
            new Bc7ModeInfo { ColorBits = 7, AlphaBits = 8, ColorIndexBits = 2, AlphaIndexBits = 0, NumPartitions = 0, HasRotation = true  },
            new Bc7ModeInfo { ColorBits = 5, AlphaBits = 6, ColorIndexBits = 2, AlphaIndexBits = 0, NumPartitions = 0, HasRotation = true  },
            new Bc7ModeInfo { ColorBits = 5, AlphaBits = 6, ColorIndexBits = 2, AlphaIndexBits = 3, NumPartitions = 0, HasRotation = false },
            new Bc7ModeInfo { ColorBits = 7, AlphaBits = 8, ColorIndexBits = 2, AlphaIndexBits = 0, NumPartitions = 0, HasRotation = false },
            new Bc7ModeInfo { ColorBits = 5, AlphaBits = 0, ColorIndexBits = 2, AlphaIndexBits = 0, NumPartitions = 6, HasRotation = false },
        };

        private static ulong Bc7ExtractBits(ulong lo, ulong hi, int offset, int count)
        {
            if (count == 0) return 0;
            if (offset + count <= 64)
                return (lo >> offset) & ((1UL << count) - 1);
            if (offset >= 64)
                return (hi >> (offset - 64)) & ((1UL << count) - 1);
            int loBits = 64 - offset;
            ulong loPart = (lo >> offset) & ((1UL << loBits) - 1);
            ulong hiPart = hi & ((1UL << (count - loBits)) - 1);
            return loPart | (hiPart << loBits);
        }

        private static int Bc7Interpolate(int a, int b, int index, int bits)
        {
            if (bits == 2)
            {
                int w = Bc7Weight2[index];
                return (a * (15 - w) + b * w + 7) >> 4;
            }
            else
            {
                int w = Bc7Weight3[index];
                return (a * (31 - w) + b * w + 15) >> 5;
            }
        }

        private static int Bc7Expand(int val, int bits)
        {
            if (bits == 8) return val;
            if (bits == 7) return (val << 1) | (val >> 6);
            if (bits == 6) return (val << 2) | (val >> 4);
            if (bits == 5) return (val << 3) | (val >> 2);
            if (bits == 4) return (val << 4) | val;
            return val;
        }

        private static void DecodeBc7Block(byte[] src, int si, byte[] dst, int di, int stride)
        {
            ulong lo = 0, hi = 0;
            for (int i = 0; i < 8; i++)
            {
                lo |= (ulong)src[si + i] << (8 * i);
                hi |= (ulong)src[si + 8 + i] << (8 * i);
            }

            int mode = -1;
            for (int i = 0; i < 8; i++)
            {
                if ((lo & (1UL << i)) != 0) { mode = i; break; }
            }

            if (mode < 0)
            {
                for (int py = 0; py < 4; py++)
                {
                    var rowDi = di + py * stride;
                    for (int px = 0; px < 4; px++)
                        SetPixel(dst, rowDi + px * 4, 0, 0, 0, 255);
                }
                return;
            }

            var info = Bc7Modes[mode];
            int bit = 8;
            int partition = 0;
            if (info.NumPartitions > 0) { partition = (int)Bc7ExtractBits(lo, hi, bit, 6); bit += 6; }
            int rotation = 0;
            if (info.HasRotation) { rotation = (int)Bc7ExtractBits(lo, hi, bit, 2); bit += 2; }
            int alphaIndexSelector = 0;
            if (mode == 5) { alphaIndexSelector = (int)Bc7ExtractBits(lo, hi, bit, 1); bit += 1; }

            int cb = info.ColorBits;
            int ab = info.AlphaBits;
            int subsetCount = (info.NumPartitions > 0) ? 2 : 1;
            int epCount = (mode == 0) ? 6 : subsetCount;
            int[,] ep = new int[6, 4];

            if (mode == 0)
            {
                for (int e = 0; e < 6; e++) ep[e, 0] = (int)Bc7ExtractBits(lo, hi, bit, 4); bit += 4;
                for (int e = 0; e < 6; e++) ep[e, 1] = (int)Bc7ExtractBits(lo, hi, bit, 4); bit += 4;
                for (int e = 0; e < 6; e++) ep[e, 2] = (int)Bc7ExtractBits(lo, hi, bit, 4); bit += 4;
                for (int e = 0; e < 6; e++) { ep[e, 3] = 255; ep[e, 0] = Bc7Expand(ep[e, 0], 4); ep[e, 1] = Bc7Expand(ep[e, 1], 4); ep[e, 2] = Bc7Expand(ep[e, 2], 4); }
            }
            else
            {
                for (int e = 0; e < epCount; e++) ep[e, 0] = (int)Bc7ExtractBits(lo, hi, bit, cb); bit += cb;
                for (int e = 0; e < epCount; e++) ep[e, 1] = (int)Bc7ExtractBits(lo, hi, bit, cb); bit += cb;
                for (int e = 0; e < epCount; e++) ep[e, 2] = (int)Bc7ExtractBits(lo, hi, bit, cb); bit += cb;
                if (ab > 0)
                {
                    for (int e = 0; e < epCount; e++) ep[e, 3] = (int)Bc7ExtractBits(lo, hi, bit, ab); bit += ab;
                    for (int e = 0; e < epCount; e++) ep[e, 3] = Bc7Expand(ep[e, 3], ab);
                }
                else
                {
                    for (int e = 0; e < epCount; e++) ep[e, 3] = 255;
                }
                for (int e = 0; e < epCount; e++) { ep[e, 0] = Bc7Expand(ep[e, 0], cb); ep[e, 1] = Bc7Expand(ep[e, 1], cb); ep[e, 2] = Bc7Expand(ep[e, 2], cb); }
            }

            int colorIndexBits = info.ColorIndexBits;
            int alphaIndexBits = info.AlphaIndexBits;
            byte[] colorIdx = new byte[16];
            byte[] alphaIdx = new byte[16];

            for (int px = 0; px < 16; px++)
            {
                colorIdx[px] = (byte)Bc7ExtractBits(lo, hi, bit, colorIndexBits); bit += colorIndexBits;
                if (alphaIndexBits > 0)
                {
                    alphaIdx[px] = (byte)Bc7ExtractBits(lo, hi, bit, alphaIndexBits); bit += alphaIndexBits;
                }
            }

            for (int py = 0; py < 4; py++)
            {
                var rowDi = di + py * stride;
                for (int px = 0; px < 4; px++)
                {
                    int pidx = py * 4 + px;
                    int subset = 0;
                    if (info.NumPartitions > 0)
                    {
                        int anchor = Bc7Anchor2[partition];
                        if (pidx == 0) subset = 0;
                        else if (pidx == anchor) subset = 1;
                        else subset = ((partition >> (pidx - 1)) & 1);
                    }
                    int ep0 = subset * 2;
                    int ep1 = ep0 + 1;
                    int r = Bc7Interpolate(ep[ep0, 0], ep[ep1, 0], colorIdx[pidx], colorIndexBits);
                    int g = Bc7Interpolate(ep[ep0, 1], ep[ep1, 1], colorIdx[pidx], colorIndexBits);
                    int b = Bc7Interpolate(ep[ep0, 2], ep[ep1, 2], colorIdx[pidx], colorIndexBits);
                    int a;
                    if (ab > 0 && alphaIndexBits > 0)
                    {
                        int ai = (mode == 5 && alphaIndexSelector == 1) ? pidx : alphaIdx[pidx];
                        a = Bc7Interpolate(ep[ep0, 3], ep[ep1, 3], ai, alphaIndexBits);
                    }
                    else if (ab > 0)
                    {
                        a = Bc7Interpolate(ep[ep0, 3], ep[ep1, 3], colorIdx[pidx], colorIndexBits);
                    }
                    else
                    {
                        a = 255;
                    }
                    if (info.HasRotation && rotation > 0)
                    {
                        int tmp;
                        if (rotation == 1) { tmp = r; r = a; a = tmp; }
                        else if (rotation == 2) { tmp = g; g = a; a = tmp; }
                        else if (rotation == 3) { tmp = b; b = a; a = tmp; }
                    }
                    SetPixel(dst, rowDi + px * 4, Clamp(r), Clamp(g), Clamp(b), Clamp(a));
                }
            }
        }

        #endregion


        #region 简单格式转换

        private static void DecodeRgb565(byte[] src, int si, byte[] dst, int di, int count)
        {
            var maxRead = src.Length;
            for (int i = 0; i < count && si + 1 < maxRead; i++, si += 2, di += 4)
            {
                Unpack565((ushort)(src[si] | (src[si + 1] << 8)),
                    out var r, out var g, out var b);
                SetPixel(dst, di, r, g, b, 255);
            }
        }

        private static void DecodeRgb888(byte[] src, int si, byte[] dst, int di, int count)
        {
            var maxRead = src.Length;
            for (int i = 0; i < count && si + 2 < maxRead; i++, si += 3, di += 4)
            {
                SetPixel(dst, di, src[si], src[si + 1], src[si + 2], 255);
            }
        }

        private static void DecodeRgba4444(byte[] src, int si, byte[] dst, int di, int count)
        {
            var maxRead = src.Length;
            for (int i = 0; i < count && si + 1 < maxRead; i++, si += 2, di += 4)
            {
                var c = (ushort)(src[si] | (src[si + 1] << 8));
                dst[di] = (byte)(((c >> 12) & 0xF) * 17);
                dst[di + 1] = (byte)(((c >> 8) & 0xF) * 17);
                dst[di + 2] = (byte)(((c >> 4) & 0xF) * 17);
                dst[di + 3] = (byte)((c & 0xF) * 17);
            }
        }

        #endregion

        #region 主调度

        private static byte[] AllocateRgbaBuffer(int width, int height)
        {
            long size = (long)width * height * 4;
            if (size > int.MaxValue)
                throw new ArgumentException($"Texture too large for decoding: {width}x{height} ({size} bytes)");
            return new byte[(int)size];
        }

        /// <summary>
        /// 将任意 MipmapFormat 数据解码为 RGBA8888。
        /// </summary>
        public static byte[] Decode(int width, int height, byte[] data, Core.Texture.MipmapFormat format)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Invalid texture dimensions: {width}x{height}");

            switch (format)
            {
                case Core.Texture.MipmapFormat.RGBA8888:
                    return data;

                case Core.Texture.MipmapFormat.RGB565:
                {
                    var out8 = AllocateRgbaBuffer(width, height);
                    DecodeRgb565(data, 0, out8, 0, width * height);
                    return out8;
                }

                case Core.Texture.MipmapFormat.RGB888:
                {
                    var out8 = AllocateRgbaBuffer(width, height);
                    DecodeRgb888(data, 0, out8, 0, width * height);
                    return out8;
                }

                case Core.Texture.MipmapFormat.RGBA4444:
                {
                    var out8 = AllocateRgbaBuffer(width, height);
                    DecodeRgba4444(data, 0, out8, 0, width * height);
                    return out8;
                }

                case Core.Texture.MipmapFormat.R8:
                {
                    var out8 = AllocateRgbaBuffer(width, height);
                    int count = width * height;
                    int maxRead = data.Length;
                    for (int i = 0, si = 0, di = 0; i < count && si < maxRead; i++, si++, di += 4)
                    {
                        out8[di] = data[si];
                        out8[di + 1] = data[si];
                        out8[di + 2] = data[si];
                        out8[di + 3] = 255;
                    }
                    return out8;
                }

                case Core.Texture.MipmapFormat.RG88:
                {
                    var out8 = AllocateRgbaBuffer(width, height);
                    int count = width * height;
                    int maxRead = data.Length;
                    for (int i = 0, si = 0, di = 0; i < count && si + 1 < maxRead; i++, si += 2, di += 4)
                    {
                        out8[di] = data[si];
                        out8[di + 1] = data[si + 1];
                        out8[di + 2] = 0;
                        out8[di + 3] = 255;
                    }
                    return out8;
                }

                case Core.Texture.MipmapFormat.CompressedDXT1:
                case Core.Texture.MipmapFormat.CompressedDXT1Alpha:
                {
                    var out8 = AllocateRgbaBuffer(width, height);
                    int si = 0;
                    for (int y = 0; y < height; y += 4)
                    for (int x = 0; x < width; x += 4, si += 8)
                    {
                        DecodeDxt1Block(data, si, out8, (y * width + x) * 4, width * 4);
                    }
                    return out8;
                }

                case Core.Texture.MipmapFormat.CompressedDXT3:
                {
                    var out8 = AllocateRgbaBuffer(width, height);
                    int si = 0;
                    for (int y = 0; y < height; y += 4)
                    for (int x = 0; x < width; x += 4, si += 16)
                    {
                        DecodeDxt3Block(data, si, out8, (y * width + x) * 4, width * 4);
                    }
                    return out8;
                }

                case Core.Texture.MipmapFormat.CompressedDXT5:
                {
                    var out8 = AllocateRgbaBuffer(width, height);
                    int si = 0;
                    for (int y = 0; y < height; y += 4)
                    for (int x = 0; x < width; x += 4, si += 16)
                    {
                        DecodeDxt5Block(data, si, out8, (y * width + x) * 4, width * 4);
                    }
                    return out8;
                }

                case Core.Texture.MipmapFormat.CompressedETC2:
                {
                    // ETC2_RGBA8: 16 bytes/block (EAC alpha 8B + ETC2 color 8B)
                    var out8 = AllocateRgbaBuffer(width, height);
                    int si = 0;
                    for (int y = 0; y < height; y += 4)
                    for (int x = 0; x < width; x += 4, si += 16)
                    {
                        DecodeEtc2A8Block(data, si, out8, (y * width + x) * 4, width * 4);
                    }
                    return out8;
                }

                case Core.Texture.MipmapFormat.CompressedBC4:
                {
                    // BC4: 8 bytes/block, 单通道灰度
                    var out8 = AllocateRgbaBuffer(width, height);
                    int si = 0;
                    for (int y = 0; y < height; y += 4)
                    for (int x = 0; x < width; x += 4, si += 8)
                    {
                        DecodeBc4Block(data, si, out8, (y * width + x) * 4, width * 4);
                    }
                    return out8;
                }

                case Core.Texture.MipmapFormat.CompressedBC7:
                {
                    // BC7: 16 bytes/block, 8种模式
                    var out8 = AllocateRgbaBuffer(width, height);
                    int si = 0;
                    for (int y = 0; y < height; y += 4)
                    for (int x = 0; x < width; x += 4, si += 16)
                    {
                        DecodeBc7Block(data, si, out8, (y * width + x) * 4, width * 4);
                    }
                    return out8;
                }

                default:
                    throw new NotSupportedException(
                        $"Mipmap format {format} cannot be decoded to RGBA8888");
            }
        }

        #endregion
    }
}
