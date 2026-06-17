# 原系统初始化流程详细分析

> 📅 分析日期：2026-06-09  
> 🎯 目的：理清原系统启动时的完整调用链，识别初始化依赖和时序问题

---

## 1. 初始化总览

原系统使用 **SendCustomEventDelayedFrames** 实现延迟初始化，形成如下依赖链：

```
场景加载
  │
  ├─ [帧0] 所有脚本的 Start() 执行
  │   ├─ 发送控制信号.Start()
  │   ├─ ControlCenter[0..N].Start()
  │   ├─ DisPlayerM.Start()
  │   ├─ FastCameraChanger.Start()
  │   ├─ AnimatorControl.Start()
  │   ├─ CameraViewControl.Start()
  │   ├─ PlayerTrackingControl.Start()
  │   ├─ PlayerTrackingSystem.Start()
  │   ├─ DefaultJSON.Start()
  │   ├─ PresetKeyBoard.Start()
  │   ├─ FastSaveOFF.Start()
  │   ├─ QuickNameChoose.Start()
  │   └─ ... 其他脚本
  │
  ├─ [帧1] SendCustomEventDelayedFrames("Start1", 1) 触发
  │   ├─ 发送控制信号.Start1()
  │   │   └─ TrackingOwner = MainControl.GetProgramVariable("TrackingOwner")
  │   ├─ ControlCenter[0..N].Start1()
  │   │   └─ 设置 material[i].material.SetTexture("_MainTex", RenderTEX)
  │   ├─ DisPlayerM.Start1()
  │   │   ├─ GetProgramVariable("CameraRender") from Main (发送控制信号)
  │   │   └─ GetProgramVariable("System") from Main
  │   ├─ CameraViewControl.Start1()
  │   │   └─ CameraDisplay.material.SetTexture("_MainTex", CameraRender[SystemIndex])
  │   ├─ PlayerTrackingControl.Start1()
  │   │   └─ 从 SystemSelect 建立所有 System 引用链
  │   └─ ...
  │
  ├─ [帧2] SendCustomEventDelayedFrames("Start1", 2) 触发
  │   ├─ FastCameraChanger.Start1()
  │   │   ├─ GetProgramVariable("DisPlayTEX") from OutputSystem (DisPlayerM)
  │   │   └─ GetProgramVariable("TVTextur") from OutputSystem
  │   └─ CameraSpaceControlSystem.Start1()
  │       └─ 建立所有 Targets[] 引用链
  │
  └─ [帧N+] 后续延迟回调
      ├─ ControlCenter.RefreashTime() → ThumbnailUpdate() 缩略图循环启动
      └─ ...
```

## 2. 关键初始化依赖链

### 2.1 显示系统初始化依赖

```
FastCameraChanger ──需要──▶ DisPlayerM ──需要──▶ 发送控制信号
     (帧2)                    (帧1)                  (帧0)
       │                        │                      │
       │ GetProgramVariable     │ GetProgramVariable    │
       │ ("DisPlayTEX")         │ ("CameraRender")      │ 拥有 CameraRender[]
       │ ("TVTextur")           │ ("System")            │ 拥有 System[]
       ▼                        ▼                      ▼
  [依赖 DisPlayerM     [依赖 发送控制信号     [最先初始化完成]
   先完成初始化]         先完成初始化]
```

**风险点**：如果 DisPlayerM.Start1()（帧1）晚于 FastCameraChanger.Start1()（帧2）——幸运的是帧1在帧2之前。但这个 1 帧的差距是脆弱的假设。

### 2.2 ControlCenter 初始化依赖

```
ControlCenter (帧0)
  │
  ├─ 查找子物体 "TrackingTarget" → TrackingCameraUdon
  ├─ 查找子物体 "CameraTranform" → CAM
  ├─ 遍历 ModName[] → 设置 TrackingIndicator
  │   └─ 需要 ModName 在 Inspector 中已配置
  │
  ├─ [帧1] Start1()
  │   ├─ 遍历 material[] → SetTexture
  │   └─ 如果 CAM.enabled → 关闭 → CallCamera()
  │       └─ 需要 VoidNameID 已设置（默认为0）
  │
  └─ RefreashTime() → 计算 RefreshTime → 启动缩略图循环
```

### 2.3 导播台初始化依赖

```
发送控制信号 (帧0) ────控制──▶ ControlCenter[0..N]
       │
       ├─ 收集 CameraRender[i] = ControlCenter[i].GetProgramVariable("RenderTEX")
       │   └─ ⚠️ 此时 ControlCenter 可能还在 Start() 中，RenderTEX 可能未赋值
       │      （但 RenderTEX 是 Inspector 配置的 public field，在 Start() 前已有值）
       │
       ├─ 设置各 ControlCenter 的 Refresh = RefreshSpeed
       │
       └─ [帧1] Start1() → 获取 TrackingOwner
```

### 2.4 预设系统初始化依赖

```
DefaultJSON.Start()
  │
  ├─ Index = 0
  ├─ List = new DataList()
  ├─ 各字段初始化为默认值
  └─ （等待用户操作 LoadToken / FromJson）

FastSaveOFF.Start()
  │
  ├─ System = Main.GetProgramVariable("System")
  ├─ 初始化 5 个并行数组（长度 = System.Length）
  └─ （等待用户操作 StartRead / StartLoad）

PresetKeyBoard.Start()
  │
  ├─ Index = 0
  ├─ List = new DataList()
  └─ Update() 中轮询 Ctrl+0~9
```

## 3. 缩略图循环初始化

缩略图循环是 ControlCenter 的核心性能优化机制，其启动流程如下：

```
ControlCenter.RefreashTime()
  │
  ├─ RefreshTime = 1 / Refresh（帧率→秒间隔）
  ├─ RandomTime = Random.Range(0, 2 * RefreshTime)  ← 随机偏移避免多相机同时渲染
  └─ SendCustomEventDelayedSeconds("ThumbnailUpdate", RandomTime)
       │
       └─ ThumbnailUpdate()
            │
            ├─ 检查 Isrun / NeedRuning / IsThumbnailOn
            ├─ 如果 !Isusing（缩略图模式）
            │   ├─ CAM 关闭 → 开启 CAM → 延迟1帧 CameraRefreash()
            │   └─ CAM 开启 → CameraRefreash()
            │
            └─ CameraRefreash()
                 └─ CAM.enabled = false → 延迟 RefreshTime 秒 → ThumbnailUpdate()
```

**状态条件**：
- `Isrun = false`（初始值）→ 缩略图循环不执行
- `Isrun = true`（当 VoidNameID > 0 时由 ModSet 设置）
- `Isusing = false`（初始值）→ 缩略图模式（定时开关 Camera）
- `Isusing = true`（由 DisPlayerM 设置为当前活跃系统时）→ 直播模式（CAM 常开）

## 4. 初始化时序问题清单

| # | 问题 | 风险 | 建议 |
|---|------|------|------|
| 1 | `DisPlayerM.Start1()` 在帧1取 `CameraRender`，但 `发送控制信号.Start1()` 也在帧1执行——两个同为帧1的延迟回调执行顺序不确定 | 中 | 使用 `SendCustomEventDelayedFrames("Start1", 2)` 或在 DisPlayerM 中增加延迟 |
| 2 | `FastCameraChanger.Start1()` 在帧2取 `DisPlayTEX`，但 `DisPlayerM.Start1()` 在帧1才赋值——跨脚本依赖1帧差距不够安全 | 中 | 增加显式的就绪检查，或在 DisPlayerM 中主动通知 |
| 3 | `PlayerTrackingControl.Start1()` 通过多层 `GetProgramVariable` 建立引用链，任何中间环节为 null 会导致后续全部失败 | 高 | 增加 null 检查和错误日志 |
| 4 | 多个 ControlCenter 同时初始化，每个都在 `RefreashTime()` 中使用随机偏移——但随机种子可能相同导致同步渲染 | 低 | 使用更分散的随机策略 |
| 5 | `ControlCenter.Start()` 中遍历 `ModName[]` 并设置 `TrackingIndicator`，但 ModName 可能为空数组 | 低 | 已有 for 循环的空数组自然跳过，但应检查 null GameObjects |

## 5. 初始化流程优化建议

### 5.1 使用显式状态机

```csharp
enum SystemInitState
{
    Uninitialized,      // 初始状态
    CoreReady,          // 核心脚本 Start() 完成
    ReferencesResolved, // 跨脚本引用解析完成
    DisplayReady,       // 显示器初始化完成
    Running,            // 正常运行
    Error               // 初始化失败
}
```

### 5.2 使用就绪标志替代延迟帧

```csharp
// 替代 SendCustomEventDelayedFrames
// 在每个脚本的 Start1 完成后设置就绪标志
// 依赖方在 Update 中检查就绪标志（最多等待 N 帧后超时）
```

### 5.3 集中初始化编排

建议在 SystemControl 中统一编排初始化流程，替代各脚本的独立 Start/Start1：

```csharp
void Start()
{
    // Phase 1: 收集所有引用（同步，帧0）
    CollectReferences();
    
    // Phase 2: 初始化显示（延迟1帧确保所有引用就绪）
    SendCustomEventDelayedFrames("InitDisplays", 1);
    
    // Phase 3: 启动缩略图（延迟2帧确保显示就绪）
    SendCustomEventDelayedFrames("StartThumbnails", 2);
    
    // Phase 4: 标记就绪
    SendCustomEventDelayedFrames("MarkReady", 3);
}
```

---

> 📝 此文档应与 SYSTEM_ANALYSIS.md 配合阅读，作为重构初始化流程的技术基准。
