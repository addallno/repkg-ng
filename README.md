# repkg — Wallpaper Engine PKG 打包/解包工具

> 基于 [notscuffed/repkg](https://github.com/notscuffed/repkg) 的社区维护分支。
> 原作者已超过 15 个月未活动，此分支**可能不会持续跟进**，甚至可能不会再有更新。请自行评估使用风险。
>
> 我们向上游提交了 PR [#73](https://github.com/notscuffed/repkg/pull/73) 包含了以下所有改动，
> 但上游长期不活跃，因此在此 fork 中维护。

[![Build](https://github.com/addallno/repkg/actions/workflows/release.yml/badge.svg)](https://github.com/addallno/repkg/actions/workflows/release.yml)
[![Release](https://img.shields.io/github/v/release/addallno/repkg?include_prereleases)](https://github.com/addallno/repkg/releases)
[![NuGet](https://img.shields.io/badge/nuget-v0.5.0--alpha-blue)](https://github.com/users/addallno/packages/nuget/package/RePKG)

## 快速安装

```sh
# 方式一：自包含二进制（免运行时，推荐）
# 从 https://github.com/addallno/repkg/releases 下载对应平台的 .zip/.tar.gz，解压即可

# 方式二：dotnet global tool（需要 .NET SDK 8.0+）
dotnet tool install --global RePKG \
  --add-source https://nuget.pkg.github.com/addallno/index.json

# 之后直接运行
repkg info ./wallpaper.pkg
```

> **关于跨架构运行**：`repkg` 命令行工具是平台相关的原生二进制，必须在对应的 CPU 架构上运行。
> 但 `RePKG.dll` 是 .NET IL 代码，**完全跨架构**——只要目标设备安装了 .NET 运行时，
> 就可以通过 `dotnet RePKG.dll <command>` 在任何架构上运行，无需重新编译。
> 例如在 Android Termux 上：
> ```sh
> cd repkg-portable  # 包含所有 .dll 文件
> dotnet RePKG.dll info wallpaper.mpkg
> ```

## 功能

### 支持的操作
| 命令 | 功能 |
|------|------|
| `extract` | 解包 `.pkg`/`.mpkg` → 文件目录；转换 `.tex` → 图片 |
| `info` | 查看 `.pkg`/`.mpkg`/`.tex` 文件信息 |
| `pack` | 目录 → `.pkg`/`.mpkg`；图片/视频 → `.tex` |
| `replace` | 替换 `.pkg`/`.mpkg` 内部文件，无需完整解包/打包 |
| `convert` | PKG/MPKG 格式互转（桌面 ↔ Android） |

### pack 命令（新增）
回应 [#72](https://github.com/notscuffed/repkg/issues/72) — 打包功能请求。

```sh
# 目录 → PKG/MPKG
repkg pack ./mywallpaper -o output.pkg          # 桌面版 (PKGV0005)
repkg pack ./mywallpaper -o output.mpkg          # Android版 (PKGM0019, 自动识别)
repkg pack ./mywallpaper --mpkg                  # Android版简写
repkg pack ./mywallpaper -o out.mpkg -m PKGV0005 # 手动指定魔术字

# 文件 → TEX 纹理
repkg pack image.png -o output.tex               # PNG → RGBA8888
repkg pack image.gif -o output.tex               # GIF → 多帧纹理
repkg pack video.mp4 -o video.tex                # MP4 → 视频纹理
repkg pack input.png -f R8 -o output.tex         # 指定格式
```

- 支持 PNG/GIF/BMP/JPEG/WebP/TGA 图片 → TEX
- 支持 MP4/WebM/MOV/AVI/MKV/FLV/WMV 视频 → TEX 视频纹理
- `-f` 参数可指定像素格式 (RGBA8888/R8/R88/BC1/BC3/BC5/BC7)

**自动 Magic 识别规则：**
- 输出后缀 `.mpkg` → 魔术字 `PKGM0019`（Android 壁纸引擎 "ID版"）
- 输出后缀 `.pkg` → 魔术字 `PKGV0005`（桌面版 Wallpaper Engine）
- 可通过 `-m` 参数覆盖

### replace 命令（新增）

```sh
# 替换单个文件
repkg replace input.mpkg -o output.mpkg -r scene.json -f ./new_scene.json

# 批量替换多个文件（-r 和 -f 按顺序配对）
repkg replace input.mpkg -o output.mpkg \
  -r scene.json -f ./new_scene.json \
  -r textures/some.tex -f ./replacement.tex

# 使用旧名输出（自动添加 .replaced 后缀）
repkg replace input.mpkg -r scene.json -f ./new_scene.json
# → input.replaced.mpkg
```

> **自动 TEX 转换**：如果替换目标是 `.tex` 条目且本地文件是视频（MP4/WebM/AVI/MOV/MKV/FLV/WMV）
> 或图片（PNG/JPEG/BMP/WebP/TGA/TIFF/GIF），会自动转换为有效的 TEX 格式。
> 视频尺寸通过 ffprobe 自动检测，也可用 `--video-width` / `--video-height` 覆盖。
>
> **注意**：`-r` 指定包内路径，`-f` 指定本地文件，两者按顺序配对。
> `-r` 的路径必须与包内条目的完整路径**完全一致**（包括子目录前缀），
> 可通过 `info` 命令查看所有条目路径。路径分隔符统一使用 `/`。

### extract / info 命令改进
- 现在支持 `.mpkg` 后缀（原版只认 `.pkg`）— 回应 [#34](https://github.com/notscuffed/repkg/issues/34)
- 目录模式下自动同时扫描 `.pkg` 和 `.mpkg`

### 视频 TEX 支持
- MP4/WebM/MOV/AVI/MKV/FLV/WMV 可打包为视频纹理
- 使用 TEXB0004 V4 容器 + V3 mipmap 混合格式，与官方一致
- Android 兼容：推荐 H.264 Baseline + yuv420p + 1920×1080

### 多框架跨平台支持
回应 [#58](https://github.com/notscuffed/repkg/issues/58)、[#29](https://github.com/notscuffed/repkg/issues/29) — Linux / 跨平台支持。

新增 `net8.0` 和 `net9.0` 目标框架，支持以下平台：
- **Windows** x64/x86/arm64
- **Linux** x64/arm64 (含 Termux/TermuxProot)
- **macOS** x64/arm64 (Apple Silicon)

```sh
# 示例：发布 Linux ARM64 单文件
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r linux-arm64 --self-contained -o ./publish

# 示例：发布 Windows x64 单文件
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r win-x64 --self-contained -o ./publish
```

### 修复的 Bug

#### TexToImageConverter 图片裁剪/缩放崩溃
回应 [#18](https://github.com/notscuffed/repkg/issues/18) — Crop rectangle should be smaller than source bounds。

原版代码在提取某些 TEX 文件时，如果裁剪矩形超出图片边界，会抛出 `ArgumentException` 导致程序崩溃退出。
修复后：先执行缩放操作（Resize）再裁剪（Crop），确保裁剪矩形总是在图片边界内。
同时移除了图片裁边（AutoOrient），提取更稳定。不再因个别错误的 TEX 文件而中断整个解包流程。

#### WriteStringI32Size 中文字符路径写入错误
回应 [#65](https://github.com/notscuffed/repkg/issues/65)、[#63](https://github.com/notscuffed/repkg/issues/63) — Size cannot be negative。

`BinaryWriter.WriteStringI32Size` 在写入字符串长度时使用了 `string.Length`（字符数），
而读取时 `ReadStringI32Size` 以字节数为准。当中文字符路径（UTF-8 多字节）出现时，
写入的长度小于实际读取需要的长度，导致后续读取错位甚至读到负数长度。
修复为 `Encoding.UTF8.GetByteCount()` 后，包含中文路径的 PKG 文件可以正确写入和读取。

#### 非 MP4 V4 纹理容器格式
当使用 V4 容器（TEXB0004）但内容不是 MP4 视频时，回退使用 V3 mipmap 数据结构，
使转换后的 TEX 格式与官方 Wallpaper Engine 输出一致。

## 使用示例

```sh
# 查看包信息
repkg info wallpaper.pkg
repkg info wallpaper.mpkg --printentries

# 解包
repkg extract wallpaper.pkg -o ./output
repkg extract wallpaper.mpkg -o ./output --no-tex-convert

# 打包
repkg pack ./output -o wallpaper.mpkg    # Android .mpkg
repkg pack ./output -o wallpaper.pkg     # 桌面 .pkg
repkg pack video.mp4                      # MP4 → video.tex
repkg pack image.png -f R8                # PNG → R8纹理
```

# 格式转换
repkg convert wallpaper.pkg -o wallpaper.mpkg                          # 桌面 → Android
repkg convert wallpaper.mpkg -o wallpaper.pkg                          # Android → 桌面
repkg convert input.mpkg -o output.mpkg --android                      # 强制指定
```

> **重要提示**：解包后修改文件再重打包时，`project.json` 必须保留在输出目录中，
> 否则打包出的 `.mpkg`/`.pkg` 在 Wallpaper Engine 中无法正确识别为可用项目。
> `project.json` 包含标题、描述、预览图、内容分级等元数据，是壁纸的必要标识文件。
> 提取时使用 `--copyproject` 参数可自动从原 PKG 所在目录复制此文件。

## 编译

### 依赖
- .NET SDK 8.0+（或 .NET Framework 4.7.2）
- 第三方库（NuGet 自动还原）：CommandLineParser, Newtonsoft.Json, SixLabors.ImageSharp

### 编译命令
```sh
git clone https://github.com/addallno/repkg
cd repkg
dotnet build RePKG/RePKG.csproj -c Debug
```

### 跨平台发布（单文件）
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

## 与上游的差异 / Issues 对应

| 改动 | 关联 Issue | 说明 |
|------|-----------|------|
| `pack` 命令（目录→PKG/MPKG，文件→TEX） | [#72](https://github.com/notscuffed/repkg/issues/72) | 社区最迫切的需求 |
| `ImageToTexConverter`（图片/GIF/视频→TEX） | [#72](https://github.com/notscuffed/repkg/issues/72) | 打包功能的核心实现 |
| V4 纹理容器写入器 + 视频 TEX | — | 视频壁纸打包支持 |
| `.mpkg` 扩展名完整支持 | [#34](https://github.com/notscuffed/repkg/issues/34) | Android 手机壁纸引擎 |
| 自动魔术字识别 | — | `.mpkg`→PKGM0019 |
| net8.0/net9.0 多框架 | [#58](https://github.com/notscuffed/repkg/issues/58), [#29](https://github.com/notscuffed/repkg/issues/29) | Linux/macOS/ARM64 跨平台 |
| `TexToImageConverter` 裁剪/缩放修复 | [#18](https://github.com/notscuffed/repkg/issues/18) | 修复 Crop 越界崩溃 |
| `WriteStringI32Size` UTF-8 长度修复 | [#65](https://github.com/notscuffed/repkg/issues/65), [#63](https://github.com/notscuffed/repkg/issues/63) | 修复 "Size cannot be negative" |
| 非 MP4 V4 回退 V3 mipmap | — | 与官方 TEX 格式对齐 |

## 许可证

MIT License — 详见 [LICENSE](LICENSE)

上游原始作者: [notscuffed](https://github.com/notscuffed)
