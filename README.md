# Auto7z (Win11 Recursive Unpacker)

## 简介 (Introduction)
Auto7z 是一款专为 Windows 11 设计的自动化解压工具。
它的核心理念是“拖入即用”：只需将压缩包拖入窗口，软件将自动尝试解压，并智能递归扫描内部内容，直到提取出最终的实质性文件（如安装包、视频、文件夹等），省去了繁琐的手动解压步骤。

## 核心功能 (Features)
- **智能递归解压**: 自动识别压缩包内的嵌套压缩包，层层剥离。
- **自动密码尝试**: 内置密码表，支持文件名作为密码，支持空密码尝试。
- **启发式扫描**: 智能判断何时停止解压（例如遇到 `.exe` 安装包或普通文件夹结构）。
- **极简 UI**: 基于 WPF 构建，支持拖拽操作与实时日志显示。
- **单文件发布**: 编译为单一 EXE，无需安装，绿色便携。

## 使用方法 (Usage)
1. 运行 `Auto7z.UI.exe`。
2. 将任意压缩文件（.zip, .7z, .rar 等）拖入主窗口。
3. 等待程序自动处理，日志窗口会显示解压进度。
4. 解压完成后，文件将出现在输出目录中。

## 配置说明 (Configuration)
- **passwords.txt**: 在程序同级目录下创建此文件，每行一个密码。程序会自动加载并尝试这些密码。

## 开发构建 (Development)

### 环境要求
- .NET 8 SDK
- Windows 11 (推荐) / Windows 10

### 构建命令
可以使用根目录下的 `Makefile` 进行自动化构建：

```bash
# 构建独立版本 (无需安装 .NET, 体积较大 ~75MB)
make publish-sc

# 构建依赖版本 (需安装 .NET 8 Runtime, 体积极小 <5MB)
make publish-fd

# 清理构建产物
make clean
```

或者手动使用 .NET CLI:
```bash
# Standalone
dotnet publish Auto7z.UI/Auto7z.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Framework Dependent
dotnet publish Auto7z.UI/Auto7z.UI.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 发布版本说明 (Releases)
在 GitHub Releases 页面，你会看到两个版本的压缩包：
1. **Auto7z-Standalone-win-x64.zip**: 独立版。解压即用，无需任何环境配置。适合普通用户。
2. **Auto7z-FrameworkDependent-win-x64.zip**: 依赖版。体积非常小，但运行前需要安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)。

## 技术栈 (Tech Stack)
- **Framework**: .NET 8
- **GUI**: WPF
- **Core**: Squid-Box.SevenZipSharp
- **Dependency**: 7z.dll

## License
MIT
