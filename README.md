# repkg — Wallpaper Engine PKG 打包/解包工具

> 基于 [notscuffed/repkg](https://github.com/notscuffed/repkg) 的社区维护分支。
> 原作者已超过 15 个月未活动，此分支**可能不会持续跟进**，甚至可能不会再有更新。请自行评估使用风险。

## 功能

### 支持的操作
| 命令 | 功能 |
|------|------|
| `extract` | 解包 `.pkg`/`.mpkg` → 文件目录；转换 `.tex` → 图片 |
| `info` | 查看 `.pkg`/`.mpkg`/`.tex` 文件信息 |
| `pack` | 目录 → `.pkg`/`.mpkg`；图片/视频 → `.tex` |

### pack 命令（新增）
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

**自动 Magic 识别规则：**
- 输出后缀 `.mpkg` → 魔术字 `PKGM0019`（Android 壁纸引擎 "ID版"）
- 输出后缀 `.pkg` → 魔术字 `PKGV0005`（桌面版 Wallpaper Engine）
- 可通过 `-m` 参数覆盖

### extract / info 命令改进
- 现在支持 `.mpkg` 后缀（原版只认 `.pkg`）
- 目录模式下自动同时扫描 `.pkg` 和 `.mpkg`

### 视频 TEX 支持
- MP4/WebM/MOV/AVI/MKV/FLV/WMV 可打包为视频纹理
- 使用 TEXB0004 V4 容器 + V3 mipmap 混合格式，与官方一致
- Android 兼容：推荐 H.264 Baseline + yuv420p + 1920×1080

### Bugfix
- **`WriteStringI32Size`**：修复中文字符路径写入时长度计算错误（写字符数而非字节数），解决包含中文的文件名导致 PKG 头损坏的问题

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
dotnet publish RePKG/RePKG.csproj -c Release -f net8.0 -r linux-arm64 --self-contained -o ./publish
```

## 使用示例

```sh
# 查看包信息
dotnet RePKG.dll info wallpaper.pkg
dotnet RePKG.dll info wallpaper.mpkg --printentries

# 解包
dotnet RePKG.dll extract wallpaper.pkg -o ./output
dotnet RePKG.dll extract wallpaper.mpkg -o ./output --no-tex-convert

# 打包
dotnet RePKG.dll pack ./output -o wallpaper.mpkg    # Android
dotnet RePKG.dll pack ./output -o wallpaper.pkg     # 桌面
dotnet RePKG.dll pack video.mp4                      # → video.tex
dotnet RePKG.dll pack image.png -f R8                # → image.tex (R8格式)
```

## 与上游的差异

新增功能：
- `pack` 命令（目录→PKG/MPKG，文件→TEX）
- `ImageToTexConverter`（图片/GIF/视频→TEX 转换器）
- V4 纹理容器写入器（TEXB0004，视频纹理支持）
- `.mpkg` 扩展名在 `info`/`extract`/`pack` 中的完整支持
- 自动魔术字识别（`.mpkg`→PKGM0019，`.pkg`→PKGV0005）
- net8.0 目标框架支持

修复：
- `WriteStringI32Size` UTF-8 字节数计算
- `TexToImageConverter` 图片裁剪/缩放逻辑
- 非 MP4 V4 容器回退 V3 mipmap 格式

## 许可证

MIT License — 详见 [LICENSE](LICENSE)

上游原始作者: [notscuffed](https://github.com/notscuffed)
