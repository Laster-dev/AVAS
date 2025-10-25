# AVAS - Advanced Visual Attack System

AVAS是一个基于C#开发的现代化远程管理系统，采用C2（Command and Control）架构，支持多协议、多监听器、多人协作的远程管理平台。

## 🚀 主要特性

### 多协议支持
- **TCP**: 传统TCP连接，稳定可靠
- **UDP**: 轻量级UDP通信
- **WebSocket**: 现代WebSocket协议
- **HTTP/HTTPS**: 基于HTTP协议的隐蔽通信
- **ICMP**: 基于ICMP协议的隐蔽通信
- **DHCP**: 利用DHCP协议的隐蔽通信
- **SMB**: 基于SMB协议的隐蔽通信

### 多监听器架构
- 支持同时运行多个不同类型的监听器
- 动态添加/删除/启动/停止监听器
- 监听器状态持久化保存
- 支持自定义绑定地址和端口

### 多人协作
- 支持多个客户端同时连接
- 实时会话管理和状态同步
- 协作式远程管理

### 插件化架构
- **Shell**: 交互式命令行执行
- **RemoteDesktop**: 远程桌面控制
- **FileManager**: 文件管理功能
- **Information**: 系统信息收集
- **ProcessManager**: 进程管理

## 📁 项目结构

```
AVAS/
├── AVASClient/          # 客户端界面 (Avalonia UI)
├── TeamServer/         # 服务端核心
├── Beacon/             # 客户端代理
├── Plugins/            # 功能插件
├── MessagePackLib/     # 消息序列化库
└── DataList/           # 数据模型
```

## 🛠️ 技术栈

- **.NET 8.0**: 跨平台运行时
- **Avalonia UI**: 现代化跨平台UI框架
- **MessagePack**: 高性能二进制序列化
- **TCP/UDP/WebSocket**: 多协议网络通信
- **插件化架构**: 可扩展的功能模块

## 🚀 快速开始

### 1. 启动服务端

```bash
# 使用默认配置启动
dotnet run --project TeamServer

# 指定Client监听端口
dotnet run --project TeamServer -- -p 8080

# 查看帮助
dotnet run --project TeamServer -- --help
```

### 2. 启动客户端界面

```bash
dotnet run --project AVASClient
```

### 3. 部署Beacon代理

```bash
# Windows版本
dotnet run --project Beacon/Beacon

# Linux版本
dotnet run --project Beacon/Linux/Beacon-Linux
```

## 📋 使用说明

### 服务端配置

服务端支持INI格式配置文件，默认配置文件为 `appconfig.ini`：

```ini
[Client]
Port=50050
BindAddress=0.0.0.0
Enabled=true

[Beacon_Listeners]
# 格式：端口=类型:启用状态
8888=TCP:false
9999=TCP:false
7777=UDP:false
8080=HTTP:false
3000=WebSocket:false
445=SMB:false
ICMP=false

[Logging]
LogLevel=Info
EnableConsoleLog=true
EnableFileLog=true
LogFilePath=logs/teamserver.log
```

### 监听器管理

通过客户端界面可以动态管理监听器：

1. **添加监听器**: 选择协议类型、绑定地址、端口
2. **启动/停止监听器**: 控制监听器运行状态
3. **删除监听器**: 移除不需要的监听器
4. **查看状态**: 实时查看所有监听器状态

### 协议选择

Beacon代理支持多种连接协议：

1. **TCP**: 最稳定的连接方式，适合内网环境
2. **UDP**: 轻量级连接，适合高延迟网络
3. **WebSocket**: 适合穿透防火墙和代理
4. **HTTP**: 伪装成正常Web流量
5. **ICMP**: 利用ICMP协议，难以被检测
6. **DHCP**: 利用DHCP协议，隐蔽性高
7. **SMB**: 利用SMB协议，适合Windows环境

### 插件功能

#### Shell插件
- 交互式命令行执行
- 支持CMD和PowerShell
- 实时输出显示
- 命令历史记录

#### RemoteDesktop插件
- 实时屏幕共享
- 鼠标键盘控制
- 多显示器支持
- 性能优化

#### FileManager插件
- 文件浏览和下载
- 文件上传功能
- 目录操作
- 文件搜索

#### Information插件
- 系统信息收集
- 硬件信息
- 网络配置
- 用户信息

## 🔧 开发指南

### 添加新插件

1. 在 `Plugins` 目录下创建新项目
2. 实现插件接口：
   ```csharp
   public class Plugin
   {
       public static string DLLINFO = "PluginName";
       private static Action<byte[]> _sendFramed;
       private static Action _destroyInstance;
       
       public Plugin(Action<byte[]> sendFramed, Action destroyInstance)
       {
           _sendFramed = sendFramed;
           _destroyInstance = destroyInstance;
       }
       
       public void Read(byte[] beaconMsgPackbyte)
       {
           // 处理接收到的数据
       }
   }
   ```

### 添加新协议

1. 在 `TeamServer/Listener` 目录下创建新的监听器类
2. 实现 `IListener` 接口
3. 在 `ListenerManager` 中注册新协议

## 📊 系统要求

### 服务端
- .NET 8.0 Runtime
- Windows/Linux/macOS

### 客户端
- .NET 8.0 Runtime
- Windows/Linux/macOS
- 图形界面支持

### Beacon代理
- .NET 8.0 Runtime
- Windows/Linux

## 🔒 安全注意事项

- 仅在授权的测试环境中使用
- 确保网络通信加密
- 定期更新和补丁
- 遵循相关法律法规

## 📝 许可证

本项目采用 MIT 许可证，详见 [LICENSE.txt](LICENSE.txt)

## 🤝 贡献

欢迎提交 Issue 和 Pull Request 来改进项目。

## 📞 支持

如有问题或建议，请通过以下方式联系：
- 提交 GitHub Issue
- 发送邮件至项目维护者

---

**免责声明**: 本工具仅用于授权的安全测试和教育目的。使用者需确保遵守相关法律法规，作者不承担任何法律责任。
