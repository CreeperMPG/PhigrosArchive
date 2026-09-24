# AGENTS.md — PhigrosArchive

TapTap SDK + Phigros 云端 API 封装，是 Phigros 工具链的**引用库/基础库**。本文件是**中性项目说明**。

## 定位

三个项目里最底层的一个，另外两个都引用它：

```
PhigrosShell ──→ 引用 PhigrosArchive
PhiShellStudio ─→ 引用 PhigrosArchive（独立 GUI，不引用 PhigrosShell）
```

| 项目 | 类型 | 目标框架 | 产出 |
|---|---|---|---|
| **PhigrosArchive**（本仓库） | 类库 | `net6.0;net10.0` | `.dll` 引用库 |
| PhigrosShell | 控制台应用 | `net10.0` | `phishell.exe`（CLI） |
| PhiShellStudio | Avalonia GUI | `net10.0` | 桌面 / Android / iOS 应用 |

> 三者是**各自独立的 git 仓库**，同级放在 `source/repos/Phigros/` 下。

## 依赖

**零 NuGet 依赖**——纯 .NET 内置 API + `System.Text.Json`。这是有意保持的：作为被两个项目引用的基础库，依赖越少越好。

## 目录结构

```
PhigrosArchive/
├── Abstractions/            ← 接口/抽象
│   ├── IDifficultyProvider.cs  — 难度/RKS 提供者接口
│   ├── ISaveLogger.cs          — 存档日志接口
│   └── SaveDataIssue.cs        — 存档数据问题定义
├── Phigros/                 ← 游戏数据
│   └── Save/                   — 存档处理
├── Utils/                   ← 工具类
│   ├── BitUtils.cs             — 位操作
│   ├── HttpUtils.cs            — HTTP 请求封装
│   └── PathUtils.cs            — 路径处理
├── PhigrosPlayerInfo.cs     ← 核心：玩家信息 + 云端 API（登录 / 存档上传下载 / 删除 / Token 刷新）
├── Taptap.cs                ← TapTap OAuth 2.0 设备码登录（QR 码流程）
└── PhigrosArchive.csproj    (net6.0;net10.0)
```

## 关键点

- **`IDifficultyProvider` 是扩展点**：消费方（如 PhiShellStudio）通过实现它来提供定数/RKS 显示，本库不硬编码数据来源。
- **多目标框架 `net6.0;net10.0`** 是为了兼容不同消费方，改动时别随手砍掉 net6.0。
- 云端 API 走 TapTap 的 OAuth 设备码流程（`Taptap.cs`），Token 刷新是 `PhigrosPlayerInfo` 的职责。

## 构建

```bash
dotnet build
```
