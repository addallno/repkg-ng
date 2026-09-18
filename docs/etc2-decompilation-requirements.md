# ETC2 RGBA8 解码器反编译需求文档

## 背景

repkg-ng 是一个 Wallpaper Engine 纹理解码工具，需要支持 Android MPKG 格式。
MPKG 中的 `.tex` 文件使用 ETC2_RGBA8 格式（format=5），对应 OpenGL 枚举 `GL_COMPRESSED_RGBA8_ETC2_EAC` (0x9278)。

我们已经有一个 C# ETC2 解码器实现，但输出质量很差（与 Python 参考实现的相关性只有 0.796，MAE 41.67）。
怀疑是 ETC2 色块的字节布局理解有误。

## 需要从 libscenejni.so 反编译获取的信息

### 1. ETC2 色块字节布局（最关键）

在 `ReadTextureDataShared` 函数（地址 0x02539d30）中，当 format=5 (ETC2_RGBA8) 时：

**问题 A：** 色块的8字节数据中，R、G、B 基色是从哪些字节的哪些位提取的？

具体需要知道：
- R 基色（5-bit）是从 byte0 的 [7:3] 还是 [4:0] 还是其他位置？
- G 基色（5-bit）是从 byte1 的 [7:3] 还是 [4:0] 还是其他位置？
- B 基色（5-bit）是从 byte2 的 [7:3] 还是 [4:0] 还是其他位置？
- dR、dG、dB 差分值是从哪些字节的哪些位提取的？

**问题 B：** table 索引（3-bit）和 diff 标志（1-bit）和 flip 标志（1-bit）在 byte3 中的精确位位置是什么？

**问题 C：** 修饰符位（modifier bits）的 MSB 和 LSB 在 bytes4-7 中的布局是什么？

### 2. 模式处理

**问题 D：** 当 diff=1 时，如何检测 T 模式、H 模式、Planar 模式？

具体需要知道：
- 检测顺序是什么？（先检查 R+Rd 溢出？还是先检查 G+Gd？）
- 每种模式的字节布局是什么？

**问题 E：** 当 diff=0（Individual 模式）时，如何提取两个子块的基色？

### 3. 修饰符查找表

**问题 F：** 程序中使用的 ETC2 修饰符查找表的精确值是什么？

需要完整的 8×4 表（8个表索引 × 4个修饰值）。

### 4. 子块分配

**问题 G：** flip 标志如何影响子块分配？

具体需要知道：
- flip=0 时，哪些像素使用 table1，哪些使用 table2？
- flip=1 时，哪些像素使用 table1，哪些使用 table2？

### 5. EAC Alpha 块

**问题 H：** EAC alpha 块的字节布局是否标准？

即：
- byte0 = baseCodeword
- byte1 = multiplier(高4位) + tableIndex(低4位)
- bytes2-7 = 48-bit 像素索引（3bit × 16像素）

像素索引的排列顺序是什么？（像素0对应哪个位？像素1对应哪个位？）

## 参考信息

### 当前错误实现的问题

我们的 C# 解码器有以下已知错误：

1. **字节3的位位置错误**：从 byte3 读取 diff 和 table 时，使用了错误的位偏移
   - 当前: diff = byte3[1], table = byte3[7:5]
   - 可能正确: diff = byte3[1], table = byte3[4:2]（但需要验证）

2. **基色提取错误**：从 byte0-2 提取基色时，使用了错误的位操作
   - 当前: R = byte0 | ((byte3 >> 5) << 5)
   - 这完全错误，应该是从 byte0 的特定位提取

3. **修饰符位索引转置**：使用了 py + px * 4 而不是 py * 4 + px

4. **缺少 T/H/Planar 模式**：只处理了差分模式和 Individual 模式

### 正确的参考实现

etcpak（wolfpld/etcpak）的 `DecodeRGBPart` 函数是正确的参考：
- 它先做字节序转换（bswap32），然后用位偏移提取字段
- 它完整支持所有5种模式：Individual、Differential、T、H、Planar

### 验证方法

如果能提供以下信息，我们可以验证正确性：
1. 用眼睛.tex（format=5, 1504x1808）中的前几个 ETC2 块的原始字节
2. 这些块解码后的 RGB 值（来自 Android 设备的实际渲染结果）

## 输出格式

请以结构化的方式回答上述问题，最好包含：
1. 每个字段的精确字节偏移和位位置
2. 关键代码片段（ARM64 汇编或伪代码）
3. 修饰符查找表的完整值
4. 模式检测的伪代码流程图
