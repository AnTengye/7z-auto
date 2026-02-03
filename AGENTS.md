# AGENTS.md - 7z-auto (Win11 Recursive Unpacker)

## 1. 项目概述 (Project Overview)
一款运行在 Windows 11 上的自动化解压工具。
核心目标：用户只需拖入一个压缩包，软件自动使用内置密码表尝试解压，并递归扫描解压后的内容。如果发现内部还有压缩包，继续解压，直到提取出“最终内容”（文件夹或可执行文件）。

## 2. 技术栈 (Tech Stack)
- **Framework**: .NET 8 (C#)
- **GUI**: WPF (Windows Presentation Foundation)
- **Core Library**: `Squid-Box.SevenZipSharp` (or standard `SevenZipSharp` compatible with .NET Core) + `7z.dll` / `7z.exe`.
- **Distribution**: Single EXE (Self-contained preferred).

## 3. 核心逻辑 (Core Logic)

### 3.1 递归解压流程 (The Loop)
1. **Input**: 接收文件路径 `SourceFile`.
2. **Password Attempt**:
   - 依次尝试 `PasswordList` 中的密码。
   - 如果遇到 `Encrypted` 但密码全失败 -> Log Error, Stop.
   - 如果解压成功 -> 输出到 `TempDir`.
3. **Content Scan (Heuristic)**:
   - 扫描 `TempDir` 内容。
   - **Case A (Terminal)**: 包含 `.exe`, `.msi`, `.bat`, `.cmd` 或只是普通文件夹结构 -> 移动到 `OutputDir`, 任务结束。
   - **Case B (Recursive)**: 内容仅包含（或主要包含）另一个压缩包 (e.g., `inner.tar`) -> 将 `inner.tar` 设为新的 `SourceFile`, 重复步骤 2。
   - **Case C (Mixed)**: 既有压缩包也有文件 -> 默认全部解压，对内部压缩包继续递归。

### 3.2 密码策略
- 预置 `passwords.txt`.
- 支持“文件名作为密码”尝试。
- 空密码优先尝试。

## 4. 架构设计 (Architecture)
- **Auto7z.UI**: WPF 前端。
  - `MainWindow.xaml`: 拖拽区，日志控制台，设置面板。
  - `ViewModels`: MVVM 模式处理 UI 逻辑。
- **Auto7z.Core**: 核心业务逻辑。
  - `ExtractorEngine`: 处理递归状态机。
  - `PasswordManager`: 管理密码表。
  - `FileAnalyzer`: 启发式分析文件类型（决定是继续解压还是停止）。

## 5. 开发规范 (Conventions)
- **Async First**: 所有 I/O 操作必须异步，严禁阻塞 UI 线程。
- **Encoding**: 默认尝试 UTF-8，失败尝试 GBK (针对老旧 rar/zip)。
- **Error Handling**: 解压失败不应崩溃，应记录日志并跳过。
- **Clean up**: 递归过程中的中间层临时文件必须在最终完成后清理。

## 6. 路线图 (Roadmap)
1. **Infrastructure**: 创建 Solution, 引入 7z 依赖。
2. **Core**: 实现 `ExtractorEngine` (单层解压 + 密码尝试)。
3. **Logic**: 实现递归状态机。
4. **UI**: 实现拖拽与实时日志绑定。
