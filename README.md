# PhigrosArchive

Phigros 云端 API 封装与工具库，为 **PhigrosShell** 和 **PhiShell Studio** 提供基础能力。

## 功能

- **TapTap OAuth 2.0 登录** — 设备码流程（QR 码扫描），获取访问令牌
- **Phigros 云端 API** — 玩家信息查询、存档上传/下载/删除、Session Token 刷新
- **存档数据处理** — 解析 Phigros 游戏存档格式
- **谱面难度/RKS 计算** — 通过 `IDifficultyProvider` 接口接入定数数据

## 目标框架

- `net6.0` — 兼容旧项目引用
- `net10.0` — 当前主力

## NuGet 依赖

无——全部使用 .NET 内置 API + `System.Text.Json`。

## 使用方法

```xml
<ProjectReference Include="..\PhigrosArchive\PhigrosArchive.csproj" />
```

### 示例

```csharp
using PhigrosArchive;

// 玩家信息 + 云端存档管理
var pinfo = new PhigrosPlayerInfo(token);
var save = await pinfo.DownloadSaveAsync();

// TapTap 登录
var taptap = new Taptap();
await taptap.LoginAsync(qrCodeUrl => {
    Console.WriteLine($"扫码登录: {qrCodeUrl}");
});
```
