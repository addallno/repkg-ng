# repkg — Wallpaper Engine PKG 打包/解包工具

> 基于 [notscuffed/repkg](https://github.com/notscuffed/repkg) 的社区维护分支。
> 原作者已超过 15 个月未活动，此分支**可能不会持续跟进**，甚至可能不会再有更新。请自行评估使用风险。
>
> 我们向上游提交了 PR [#73](https://github.com/notscuffed/repkg/pull/73) 包含了以下所有改动，
> 但上游长期不活跃，因此在此 fork 中维护。

---

# repkg — Wallpaper Engine PKG Pack/Unpack Tool

> A community-maintained fork of [notscuffed/repkg](https://github.com/notscuffed/repkg).
> The original author has been inactive for over 15 months. This fork **may not be continuously maintained** — use at your own risk.
>
> We submitted PR [#73](https://github.com/notscuffed/repkg/pull/73) upstream with all changes below,
> but the upstream repo has been inactive for a long time, so we maintain it here.

[![Build](https://github.com/addallno/repkg/actions/workflows/release.yml/badge.svg)](https://github.com/addallno/repkg/actions/workflows/release.yml)
[![Release](https://img.shields.io/github/v/release/addallno/repkg?include_prereleases)](https://github.com/addallno/repkg/releases)
[![NuGet](https://img.shields.io/badge/nuget-v0.5.1--alpha-blue)](https://github.com/users/addallno/packages/nuget/package/RePKG)

## 快速安装 / Quick Install

```sh
# 方式一：自包含二进制（免运行时，推荐）
# 从 https://github.com/addallno/repkg/releases 下载对应平台的 .zip/.tar.gz，解压即可

# Option 1: Self-contained binary (no runtime needed, recommended)
# Download the .zip/.tar.gz for your platform from https://github.com/addallno/repkg/releases

# 方式二：dotnet global tool（需要 .NET SDK 8.0+）
# Option 2: dotnet global tool (requires .NET SDK 8.0+)
dotnet tool install --global RePKG \
  --add-source https://nuget.pkg.github.com/addallno/index.json

# 之后直接运行 / Then run directly
repkg info ./wallpaper.pkg
```

> **关于跨架构运行 / Cross-architecture usage**：
> `repkg` 命令行工具是平台相关的原生二进制，必须在对应的 CPU 架构上运行。
> 但 `RePKG.dll` 是 .NET IL 代码，**完全跨架构**——只要目标设备安装了 .NET 运行时，
> 就可以通过 `dotnet RePKG.dll <command>` 在任何架构上运行，无需重新编译。
>
> The `repkg` CLI is a platform-specific native binary, but `RePKG.dll` is .NET IL code
> that works on any architecture with the .NET runtime installed. For example, on Android Termux:
>
> ```sh
> cd repkg-portable  # 包含所有 .dll 文件 / Contains all .dll files
> dotnet RePKG.dll info wallpaper.mpkg
> ```

## 功能 / Features

### 支持的操作 / Supported Commands

| 命令 / Command | 功能 / Description |
|------|------|
| `extract` | 解包 `.pkg`/`.mpkg` → 文件目录；转换 `.tex` → 图片 |
| `info` | 查看 `.pkg`/`.mpkg`/`.tex` 文件信息 |
| `repack` | 目录 → `.pkg`/`.mpkg`；图片/视频 → `.tex` |
| `replace` | 替换 `.pkg`/`.mpkg` 内部文件，无需完整解包/打包 |
| `convert` | PKG/MPKG 格式互转（桌面 ↔ Android） |

### repack 命令 / repack Command

回应 [#72](https://github.com/notscuffed/repkg/issues/72) — 打包功能请求。

```sh
# 目录 → PKG/MPKG
# Directory → PKG/MPKG
repack ./mywallpaper -o output.pkg          # 桌面版 / Desktop (PKGV0005)
repack ./mywallpaper -o output.mpkg          # Android版 / Android (PKGM0019)
repack ./mywallpaper -M                      # Android版简写 / Android shorthand
repack ./mywallpaper -o out.mpkg -m PKGV0005 # 手动指定魔术字 / Manual magic

# 文件 → TEX 纹理 / File → TEX texture
repack image.png -o output.tex               # PNG → RGBA8888
repack image.gif -o output.tex               # GIF → 多帧纹理 / multi-frame
repack video.mp4 -o video.tex                # MP4 → 视频纹理 / video texture
repack input.png -f R8 -o output.tex         # 指定格式 / Specify format
```

- 支持 PNG/GIF/BMP/JPEG/WebP/TGA 图片 → TEX
- 支持 MP4/WebM/MOV/AVI/MKV/FLV/WMV 视频 → TEX 视频纹理
- `-f` 参数可指定像素格式 (RGBA8888/R8/RG88)

**自动 Magic 识别规则 / Auto-magic rules：**
- 输出后缀 `.mpkg` → 魔术字 `PKGM0019`（Android 壁纸引擎 "ID版"）
- 输出后缀 `.pkg` → 魔术字 `PKGV0005`（桌面版 Wallpaper Engine）
- 可通过 `-m` 参数覆盖 / Override with `-m`

### replace 命令 / replace Command

```sh
# 替换单个文件 / Replace a single file
repack input.mpkg -o output.mpkg -t scene.json -f ./new_scene.json

# 批量替换多个文件 / Batch replace multiple files (-t and -f paired by order)
repack input.mpkg -o output.mpkg \
  -t scene.json -f ./new_scene.json \
  -t textures/some.tex -f ./replacement.tex

# 使用旧名输出（自动添加 .replaced 后缀）
# Output with old name (auto-adds .replaced suffix)
repack input.mpkg -t scene.json -f ./new_scene.json
# → input.replaced.mpkg
```

> **自动 TEX 转换 / Auto TEX conversion**：如果替换目标是 `.tex` 条目且本地文件是视频（MP4/WebM/AVI/MOV/MKV/FLV/WMV）
> 或图片（PNG/JPEG/BMP/WebP/TGA/TIFF/GIF），会自动转换为有效的 TEX 格式。
> 视频尺寸通过 ffprobe 自动检测，也可用 `--video-width` / `--video-height` 覆盖。
>
> `-t` 指定包内路径，`-f` 指定本地文件，两者按顺序配对。
> `-t` 的路径必须与包内条目的完整路径**完全一致**（包括子目录前缀），
> 可通过 `info` 命令查看所有条目路径。路径分隔符统一使用 `/`。

### extract / info 命令改进 / extract / info improvements

- 现在支持 `.mpkg` 后缀（原版只认 `.pkg`）— 回应 [#34](https://github.com/notscuffed/repkg/issues/34)
- 目录模式下自动同时扫描 `.pkg` 和 `.mpkg`

### 视频 TEX 支持 / Video TEX Support

- MP4/WebM/MOV/AVI/MKV/FLV/WMV 可打包为视频纹理
- 使用 TEXB0004 V4 容器 + V3 mipmap 混合格式，与官方一致
- Android 兼容：推荐 H.264 Baseline + yuv420p + 1920×1080

### 多框架跨平台支持 / Multi-target Cross-platform

回应 [#58](https://github.com/notscuffed/repkg/issues/58)、[#29](https://github.com/notscuffed/repkg/issues/29) — Linux / 跨平台支持。

新增 `net8.0` 和 `net9.0` 目标框架，支持以下平台：
- **Windows** x64/x86/arm64
- **Linux** x64/arm64 (含 Termux/TermuxProot)
- **macOS** x64/arm64 (Apple Silicon)

```sh
# 示例：发布 Linux ARM64 单文件 / Example: Publish Linux ARM64 single-file
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r linux-arm64 --self-contained -o ./publish

# 示例：发布 Windows x64 单文件 / Example: Publish Windows x64 single-file
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r win-x64 --self-contained -o ./publish
```

## 使用示例 / Examples

```sh
# 查看包信息 / View package info
repack info wallpaper.pkg
repack info wallpaper.mpkg -e

# 解包 / Extract
repack extract wallpaper.pkg -o ./output
repack extract wallpaper.mpkg -o ./output --no-tex-convert

# 打包 / Repack
repack ./output -o wallpaper.mpkg    # Android .mpkg
repack ./output -o wallpaper.pkg     # Desktop .pkg
repack video.mp4                      # MP4 → video.tex
repack image.png -f R8                # PNG → R8 texture

# 格式转换 / Format conversion
repack convert wallpaper.pkg -o wallpaper.mpkg          # Desktop → Android
repack convert wallpaper.mpkg -o wallpaper.pkg          # Android → Desktop
repack convert input.mpkg -o output.mpkg -M              # Force Android format
```

> **重要提示 / Important**：解包后修改文件再重打包时，`project.json` 必须保留在输出目录中，
> 否则打包出的 `.mpkg`/`.pkg` 在 Wallpaper Engine 中无法正确识别为可用项目。
> `project.json` 包含标题、描述、预览图、内容分级等元数据，是壁纸的必要标识文件。
> 提取时使用 `-c` 参数可自动从原 PKG 所在目录复制此文件。

## 修复的 Bug / Fixed Bugs

### ETC2 RGBA8 Alpha 通道被覆盖
所有 ETC2_RGBA8 纹理的透明度信息之前被错误清零，现已修复为正确保留 alpha 通道。

All ETC2_RGBA8 textures previously had their alpha channel incorrectly zeroed out. Now fixed to properly preserve alpha.

### int 溢出导致大文件崩溃
`width * height * 4` 等整数运算在大纹理上溢出，导致分配错误大小的缓冲区。现在使用 `long` 计算并检查范围。

Integer arithmetic like `width * height * 4` overflowed on large textures, causing incorrect buffer allocation. Now uses `long` with range checks.

### PackageEntry Offset/Length 使用 long
所有包文件偏移量和长度从 `int` 改为 `long`，支持超过 2GB 的包文件。

All package file offsets and lengths changed from `int` to `long` to support packages over 2GB.

### TexToImageConverter 图片未 dispose
`ConvertToImage` 和 `ConvertToGif` 中创建的 `Image` 对象现在正确释放，避免内存泄漏。

`Image` objects created in `ConvertToImage` and `ConvertToGif` are now properly disposed to prevent memory leaks.

### 负数 byteCount 绕过安全检查
恶意/损坏的 TEX 文件中负数 byteCount 之前绕过所有安全检查，现已添加下界校验。

Negative byte counts in malicious/corrupted TEX files previously bypassed all safety checks. Now validated.

### LZ4 解压结果验证
`LZ4Codec.Decode` 返回值之前被忽略，现在验证解压字节数是否匹配预期。

The `LZ4Codec.Decode` return value was previously ignored. Now validates decompressed byte count matches expected.

### 多处空引用修复
- `Extract.cs`: `Path.GetDirectoryName` 返回 null 时的崩溃
- `Replace.cs`: `FirstImage`/`FirstMipmap` 空引用链
- `Repack.cs`: `Substring` 无边界保护
- `TexToImageConverter`: `FirstImage` null 检查
- `TexFrameInfoContainerReader`: `Frames[0]` 空列表越界

### 大小写不敏感扩展名比较
`Replace`/`Convert`/`Extract` 中的扩展名比较现在使用 `OrdinalIgnoreCase`。

Extension comparisons in `Replace`/`Convert`/`Extract` now use `OrdinalIgnoreCase`.

### BC7 解码改进
锚点表、分区数、旋转处理的修复，提高了 BC7 纹理解码准确性。

Anchor table, partition count, and rotation handling fixes improve BC7 texture decoding accuracy.

## 编译 / Build

### 依赖 / Dependencies
- .NET SDK 8.0+（或 .NET Framework 4.7.2）
- 第三方库（NuGet 自动还原）：CommandLineParser, Newtonsoft.Json, SixLabors.ImageSharp

### 编译命令 / Build Commands
```sh
git clone https://github.com/addallno/repkg
cd repkg
dotnet build RePKG/RePKG.csproj -c Debug
```

### 跨平台发布（单文件）/ Cross-platform Publish (single-file)
```sh
# Windows x64
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r win-x64 --self-contained -o ./publish

# Linux x64
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r linux-x64 --self-contained -o ./publish

# Linux ARM64 (Android Termux)
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r linux-arm64 --self-contained -o ./publish

# macOS Apple Silicon
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r osx-arm64 --self-contained -o ./publish
```

## 与上游的差异 / Upstream Differences

| 改动 / Change | 关联 Issue | 说明 / Description |
|------|-----------|------|
| `repack` 命令 | [#72](https://github.com/notscuffed/repkg/issues/72) | 目录→PKG/MPKG，文件→TEX |
| `ImageToTexConverter` | [#72](https://github.com/notscuffed/repkg/issues/72) | 图片/GIF/视频→TEX |
| V4 纹理容器写入器 + 视频 TEX | — | 视频壁纸打包支持 |
| `.mpkg` 扩展名支持 | [#34](https://github.com/notscuffed/repkg/issues/34) | Android 手机壁纸引擎 |
| 自动魔术字识别 | — | `.mpkg`→PKGM0019 |
| net8.0/net9.0 多框架 | [#58](https://github.com/notscuffed/repkg/issues/58), [#29](https://github.com/notscuffed/repkg/issues/29) | Linux/macOS/ARM64 跨平台 |
| `TexToImageConverter` 裁剪/缩放修复 | [#18](https://github.com/notscuffed/repkg/issues/18) | 修复 Crop 越界崩溃 |
| `WriteStringI32Size` UTF-8 长度修复 | [#65](https://github.com/notscuffed/repkg/issues/65), [#63](https://github.com/notscuffed/repkg/issues/63) | 修复中文路径写入 |
| 非 MP4 V4 回退 V3 mipmap | — | 与官方 TEX 格式对齐 |
| ETC2 RGBA8 alpha 修复 | — | 正确保留透明度信息 |
| int 溢出修复（大纹理/大文件） | — | 支持 2GB+ 包文件 |
| BC7 解码改进 | — | 锚点表/分区/旋转修复 |

## 许可证 / License

MIT License — 详见 / See [LICENSE](LICENSE)

上游原始作者 / Original author: [notscuffed](https://github.com/notscuffed)
