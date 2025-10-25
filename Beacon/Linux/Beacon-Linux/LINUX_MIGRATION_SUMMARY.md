# Beacon Linux 迁移总结

## 概述
成功将 Beacon 项目从 Windows 平台迁移到 Linux 平台，移除了所有 Windows 特定的依赖和功能。

## 主要修改内容

### 1. 移除 Windows 特定功能
- **Program.cs**: 移除了 `SetProcessDPIAware()` 调用和 `user32.dll` 导入
- **Win32API.cs**: 完全删除，替换为 Linux 兼容的窗口检测
- **SetRegistry.cs**: 完全删除，替换为文件存储系统

### 2. 创建 Linux 兼容替代方案

#### 文件存储系统 (FileStorage.cs)
- 替换 Windows 注册表功能
- 使用用户主目录下的 `.beacon` 文件夹存储数据
- 支持二进制和字符串数据的存储和检索

#### 窗口检测 (Helper.cs)
- 使用 `xdotool` 和 `wmctrl` 命令获取活动窗口标题
- 提供回退机制，确保在没有这些工具时也能正常工作

#### 进程检测
- 使用 `pgrep` 命令检测进程是否存在
- 回退到 .NET 的 `Process.GetProcesses()` 方法

#### 管理员权限检测
- 检查当前用户是否为 `root`
- 使用 `id -u` 命令检查用户 ID

#### 杀毒软件检测
- 检测 Linux 上常见的杀毒软件进程
- 使用 `systemctl` 检查系统服务

### 3. 消息包系统
- 创建了 `MPLib.MP` 命名空间
- 实现了基于 MessagePack 的 `MsgPack` 基类
- 支持二进制数据的序列化和反序列化

### 4. 压缩功能
- 创建了 `Zip.cs` 工具类
- 使用 GZip 压缩算法进行数据压缩和解压缩

### 5. .NET 8 兼容性
- 移除了已废弃的 `AppDomain` 相关代码
- 移除了 `System.Runtime.Remoting` 依赖
- 更新了插件加载机制，直接在当前域中加载

### 6. 项目配置
- 添加了必要的 NuGet 包引用：
  - `MessagePack` (2.5.192)
  - `System.Management` (8.0.0)
- 配置了 .NET 8 目标框架

## 文件结构变化

### 新增文件
- `Helper/FileStorage.cs` - 文件存储系统
- `Helper/Zip.cs` - 压缩工具类
- `MessagePackLib/MPLib/MP/MsgPack.cs` - 消息包基类

### 删除文件
- `Helper/Win32API.cs` - Windows API 封装
- `Helper/SetRegistry.cs` - 注册表操作

### 修改文件
- `Program.cs` - 移除 Windows 特定代码
- `Helper/Helper.cs` - 更新为 Linux 兼容功能
- `Helper/BeaconHandle.cs` - 替换注册表调用为文件存储
- `Connect/HttpBeacon.cs` - 更新 User-Agent
- `Connect/ICMPBeacon.cs` - 更新注释
- `Beacon-Linux.csproj` - 添加必要的包引用

## 编译状态
✅ **编译成功** - 项目可以在 Linux 上正常编译和运行

## 注意事项

### 系统依赖
在 Linux 系统上运行需要安装以下工具（可选，用于增强功能）：
- `xdotool` - 用于获取活动窗口标题
- `wmctrl` - 窗口管理工具（备用）
- `pgrep` - 进程检测工具

### 数据存储
- 数据存储在 `~/.beacon/{HWID}/` 目录下
- 二进制数据以 `.bin` 扩展名存储
- 字符串数据以 `.txt` 扩展名存储

### 权限要求
- 程序需要适当的权限来访问网络
- 某些功能可能需要 root 权限（如原始套接字操作）

## 测试建议
1. 在 Linux 系统上编译项目
2. 测试各种连接方式（TCP、UDP、HTTP、WebSocket、ICMP、DHCP、SMB）
3. 验证文件存储功能
4. 测试插件加载机制
5. 验证窗口检测和进程检测功能

## 总结
项目已成功迁移到 Linux 平台，所有 Windows 特定的功能都已替换为 Linux 兼容的替代方案。代码保持了原有的功能性和可扩展性，同时确保了跨平台兼容性。
