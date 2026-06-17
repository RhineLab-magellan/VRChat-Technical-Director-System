# VRChatFansTDsystem 系统设计方案

> 📅 设计日期：2026-06-09  
> 🎯 目标：对原 VRChat Technical Director System 进行框架重构，建立模块化、类型安全的架构  
> 🛠️ SDK：VRChat World SDK 3.x + UdonSharp  
> 🎮 Unity：2022.3.22f1

---

## 1. 架构总览

### 1.1 设计原则

| 原则 | 说明 |
|------|------|
| **直接引用优先** | 除子系统 ↔ 点位系统保留 `SetProgramVariable`/`SendCustomEvent` 外，全部使用 Inspector 直接引用 |
| **中心化决策** | 满帧率控制、系统切换由 `SystemControl` 统一判定和下发 |
| **组件注册中心** | `VRCTDCamera` 持有所有子系统所需组件引用，子系统不再通过 `parent.Find(...)` 查找 |
| **平面化命名** | CAM 后缀 = CameraSystem 侧逻辑，Plane 后缀 = 控制面板 UI，无后缀 = 独立系统 |

### 1.2 变量组织规范

所有脚本（CAM / Plane / 独立系统）內部变量按以下 **四区标准** 顺序组织：

| 区域 | 说明 | 包含内容 | 示例 |
|------|------|---------|------|
| **1: 组件注册** | Inspector 直接引用的其他组件/脚本 | Camera、Animator、UdonBehaviour 引用、Renderer 等 | `public Camera mainCamera;` |
| **2: 同步变量** | `[UdonSynced]` 网络同步字段 | Manual/Continuous 同步的 int/float/bool/Vector3/string | `[UdonSynced] private int _voidNameID;` |
| **3: 可设置属性** | 非同步的公开属性，供直接引用调用 | Inspector 配置项、运行时状态、子系统的四标志 | `public bool NeedRuning;` |
| **4: UI 依赖** | 控制面板引用的 UI 组件 | Slider、Text、Dropdown、Toggle、InputField、Image 等 | `public Slider FOVControl;` |

> **规则**：
> - CAM 脚本包含区域 1、2、3（无 UI 依赖）
> - Plane 脚本包含区域 1、4（无同步变量，纯本地 UI）
> - 独立系统按需包含 1、2、3（如 CameraOutputSystem 有 Manual 同步）
> - 区域 3 中的 Inspector 配置项在上方、运行时私有状态在下方，用注释分隔

### 1.3 系统分层

```
┌─────────────────────────────────────────────────┐
│              控制面板层 (Plane)                    │
│  SystemControl | FovControlPlane                 │
│  CameraControlPlane | TrackingPlayerPlane        │
├─────────────────────────────────────────────────┤
│              相机核心层 (CAM)                      │
│  VRCTDCamera（组件注册中心 + 生命周期管理）         │
│  FovControlCAM | CameraControlCAM                │
│  TrackingPlayerCAM                               │
├─────────────────────────────────────────────────┤
│              子系统层 (SubControlSystem)           │
│  SubControlSystem（基类：四标志 + 生命周期）        │
│  └─ 点位系统（SetProgramVariable/SendCustomEvent）│
├─────────────────────────────────────────────────┤
│              独立系统                             │
│  CameraOutputSystem（显示输出管理）                │
└─────────────────────────────────────────────────┘
```

### 1.4 文件清单

| # | 文件 | 文件夹 | 后缀 | 说明 |
|---|------|--------|------|------|
| 1 | `VRCTDCamera.cs` | `相机系统/` | CAM | 单相机核心：组件注册中心、Manual 同步、Camera 生命周期 |
| 2 | `SubControlSystem.cs` | `相机系统/` | — | 点位子系统基类：四标志、生命周期定义 |
| 3 | `CameraOutputSystem.cs` | `图像输出系统/` | — | 显示输出管理（Manual 同步）：双纹理动画过渡、Animator 驱动、批量显示器初始化 |
| 4 | `FovControlCAM.cs` | `相机控制系统/` | CAM | FOV 执行：速率/缓冲/插值模式、FOV 限制器、手柄响应、`isPlaneListening` 回调守卫 |
| 5 | `FovControlPlane.cs` | `相机控制系统/` | Plane | FOV 控制面板：Slider/Toggle UI + 多 CAM 切换（cameraIndex） |
| 6 | `CameraControlCAM.cs` | `相机控制系统/` | CAM | 相机位姿微调：localPosition/Rotation 的 Manual 同步、0.5s 序列化冷却、`isPlaneListening` 回调守卫 |
| 7 | `CameraControlPlane.cs` | `相机控制系统/` | Plane | 位姿控制面板：6 轴 Slider + 速度 Slider + 重置/停止按钮 + 多 CAM 切换（cameraIndex） |
| 8 | `TrackingPlayerCAM.cs` | `跟踪控制系统/` | CAM | 玩家跟踪执行：TrackingDataType、偏移、相对/绝对模式、旋转锁、NetworkCallable 同步 |
| 9 | `TrackingPlayerPlane.cs` | `跟踪控制系统/` | Plane | 跟踪配置面板 + 玩家名快速选择 + 多 CAM 切换（cameraIndex） + TempWrite 模式 |
| 10 | `SystemControl.cs` | `相机控制面板/` | Plane | 导播主面板：NoVariableSync、多 VRCTDCamera 管理、满帧率中心判定、参数下发 |

**已创建**：10 个脚本 + 对应 .asset 文件，均为空壳。

---

## 2. 核心组件设计

### 2.1 VRCTDCamera — 单相机核心

**同步模式**：`BehaviourSyncMode.Manual`  
**安装位置**：CameraSystem GameObject

#### 组件注册中心

VRCTDCamera 通过 Inspector 直接引用持有所有子系统需要的组件，子系统通过 `VRCTDCamera` 引用获取，不再通过 `transform.parent.Find(...)` 查找：

```
VRCTDCamera Inspector 引用:
  ├─ Camera mainCamera              ← 主渲染相机
  ├─ Transform cameraTransform     ← CameraTranform
  ├─ Transform trackingTarget      ← TrackingTarget
  ├─ RenderTexture renderTexture   ← 输出目标
  ├─ MeshRenderer[] displayMaterials← 缩略图显示器
  ├─ SubControlSystem[] subsystems ← 子系统数组
  ├─ FovControlCAM fovControlCAM   ← FOV执行引用
  ├─ CameraControlCAM cameraControlCAM ← 位姿执行引用
  └─ TrackingPlayerCAM trackingPlayerCAM ← 跟踪执行引用
```

#### 同步变量

| 变量 | 类型 | 说明 |
|------|------|------|
| `voidNameID` | int | 当前激活的子系统索引（0=关闭，范围 [0, subsystems.Length]） |
| `cameraTrackingTarget` | int | 当前锚点索引（范围 [0, Targets.Length-1]，由子系统最⼤锚点数决定） |
| `playerID` | int | 跟踪目标玩家的 VRCPlayerApi.playerId（反序列化时通过 ID → VRCPlayerApi → displayName 还原；若 GetPlayerById 返回 null 则维持当前目标不变） |
| `slarp` | bool | 缓动开关 |
| `voidObjectActive` | bool | 关联对象激活状态 |

#### 边界检查

以下所有 int 值在通过按钮（Up/Down）而非 Dropdown/Toggle 控制时，**写入前和反序列化后**均需边界检查，防止数组越界：

| 变量 | 所属 | 有效范围 | 越界处置 |
|------|------|---------|---------|
| `voidNameID` | VRCTDCamera | `[0, subsystems.Length]` | 0（关闭） |
| `cameraTrackingTarget` | VRCTDCamera | `[0, currentSubsystem.Targets.Length - 1]` | 0 |
| `trackingID` | TrackingPlayerCAM | `[0, 3]`（对应 Head/LeftHand/RightHand/Origin） | 0（Head） |
| `cameraIndex` | FovControlPlane / CameraControlPlane / TrackingPlayerPlane | `[0, camArray.Length - 1]` | 不变（拒绝越界操作） |

| `playerID` | VRCTDCamera | `GetPlayerById(id) != null && IsValid()` | 维持当前目标 |

```
通用边界检查模式:

  写入前（SystemControl / Plane 调用）:
    value = Mathf.Clamp(value, min, max);

  反序列化后（OnDeserialization）:
    if (value < min || value > max) → value = fallback;

  按钮 Up/Down 操作时:
    if (++value > max) value = 0;  // 循环回到头
    if (--value < min) value = max; // 循环回到尾
    // 或: 钳制在边界不动
```

> **注意**：`CameraOutputSystem` 的 `currentTexIndex` / `nextTexIndex` 使用 Toggle/Dropdown 方案，由其自身保证有效性；`animationIndex` 由 Animator 内部管理，不在检查范围内。

子系统的 `_ChangerTarget()` 内部同样需要二次校验（防御性）：`if (CameraTrackingTarget > Targets.Length - 1) return;`

#### 主要方法

| 方法 | 说明 |
|------|------|
| `Start()` | 收集 Inspector 引用 → 初始化所有子系统 |
| `OnDeserialization()` | 同步回调 → `_CallCamera()` |
| `_StartChanger()` | 接收 SystemControl 指令 → SetOwner + RequestSerialization |
| `SetFullFrameRate(bool)` | 接收 SystemControl 的满帧率指令 → 控制 Camera.enabled |
| `_RefreshRenderFrame()` | 被 SystemControl 的缩略图循环调用 → 开启 Camera 渲染一帧后关闭 |

---

### 2.2 SubControlSystem — 点位子系统基类

**同步模式**：`BehaviourSyncMode.None`（纯本地执行）  
**安装位置**：CameraSystem GameObject 的子节点（作为 `VRCTDCamera.subsystems[]` 元素）

#### 四标志配置

| 标志 | 类型 | 配置方式 | 消费者 | 说明 |
|------|------|---------|--------|------|
| `NeedRuning` | bool | Inspector | `SystemControl`（缩略图循环中读取） | 子系统是否需要 Camera 持续渲染（满帧率），禁止缩略图 |
| `SpecialSignal` | bool | Inspector | 子系统 `_ChangerTarget()` | 切换点位时是否通知点位附 Udon |
| `NeedCallBack` | bool | Inspector | 子系统 `Start()` | 初始化时是否将自己注册到点位的 `"Managerudon"` 变量 |
| `FOVDefectUse` | bool | Inspector | 子系统 `_ChangerTarget()` | 切换点位时是否从点位读取预设 FOV 并推送到 `FovControlCAM` |

#### 四标志协同逻辑

```
Start():
  if (NeedCallBack):
    遍历 SystemUdon[] → SetProgramVariable("Managerudon", this)
    点位获得回调子系统的能力

_ChangerTarget():
  激活 Targets[CameraTrackingTarget]
  
  if (SpecialSignal):
    启用点位附Udon
    SendCustomEvent("_OnPointActivated")
    SetProgramVariable 写入运行时参数
  
  if (FOVDefectUse):
    float fov = pointUdon.GetProgramVariable("FOV")  ← GetProgram（点位通信保留）
    _camera.fovControlCAM.ApplyDefectFOV(fov)       ← 直接引用（CAM通信走直接引用）

SystemControl 读取（缩略图循环中）:
  NeedRuning → 与 _fullFrameRate[i] 取 OR → 决定该 Camera 是否允许缩略图
```

#### 典型配置

| 子系统 | NeedRuning | SpecialSignal | NeedCallBack | FOVDefectUse |
|--------|-----------|---------------|-------------|--------------|
| 固定锚点 | false | true | true | true |
| 手动追踪 | true | true | true | false |
| 无人机 | true | false | false | false |
| 简单固定机位 | false | false | false | false |

#### 从 VRCTDCamera 获取的引用

| 引用 | 路径 | 方式 |
|------|------|------|
| Camera | `_camera.mainCamera` | 直接引用 |
| Transform | `_camera.cameraTransform` | 直接引用 |
| TrackingTarget | `_camera.trackingTarget` | 直接引用 |
| RenderTexture | `_camera.renderTexture` | 直接引用 |
| DisplayMaterials | `_camera.displayMaterials` | 直接引用 |
| FovControlCAM | `_camera.fovControlCAM` | 直接引用 |
| CameraControlCAM | `_camera.cameraControlCAM` | 直接引用 |
| SlarpV | `_camera.slarpV` | 直接属性 |

---

### 2.3 SystemControl — 导播主面板

**同步模式**：`BehaviourSyncMode.None`（纯本地 UI）  
**安装位置**：控制面板 GameObject

#### 主要职责

1. **多系统管理**：维护 `VRCTDCamera[]` 直接引用
2. **参数下发**：将 UI 操作结果通过直接引用调用 `VRCTDCamera._StartChanger()` 等
3. **满帧率中心判定**：从 `CameraOutputSystem` 获取当前播放索引，统一判定哪些系统需要满帧率
4. **缩略图循环管理**：以配置帧率驱动缩略图循环，每轮检查所有条件

#### Inspector 配置

```
SystemControl Inspector:
  ├─ VRCTDCamera[] cameras            ← 所有相机系统
  ├─ CameraOutputSystem outputSystem  ← 显示输出系统
  ├─ float thumbnailRefreshRate       ← 缩略图刷新帧率（次/秒），默认 1f
  └─ （内部维护）
      ├─ bool[] _fullFrameRate        ← 满帧率标志数组（长度 = cameras.Length）
      ├─ float _thumbnailDelay        ← 换算后的延迟秒数 = 1f / thumbnailRefreshRate
      └─ bool _isThumbnailActive      ← 缩略图循环是否正在运行
```

#### 缩略图循环设计

```
初始化:
  _thumbnailDelay = 1f / thumbnailRefreshRate
  → SendCustomEventDelayedSeconds("_ThumbnailLoop", _thumbnailDelay)

_ThumbnailLoop() — 每轮执行:
  1. 遍历所有 VRCTDCamera[i]:
     ├─ 检查 _fullFrameRate[i]（当前是否被输出系统选中）
     ├─ 检查 VRCTDCamera[i].currentSubsystem.NeedRuning（子系统是否需要满帧率）
     └─ 如果任一条件 = true:
         → 该 Camera 必须满帧率运行（Camera.enabled = true）
         → 该 Camera 不参与本轮缩略图
  
  2. 对所有允许缩略图的 Camera:
     ├─ Camera.enabled = true（渲染一帧到 RenderTexture）
     └─ 延迟 1 帧 → Camera.enabled = false
  
  3. 如果存在任何不允许缩略图的 Camera:
     → 缩略图循环继续（那些 Camera 保持满帧率，其余轮询渲染）
  
  4. 如果所有 Camera 都允许缩略图:
     → 缩略图正常循环
  
  5. SendCustomEventDelayedSeconds("_ThumbnailLoop", _thumbnailDelay)
     → 循环自驱动
```

#### 缩略图退出信号（满帧率触发条件）

以下任一信号置位时，对应 Camera 退出缩略图、进入满帧率运行：

| # | 信号来源 | 触发条件 | 影响范围 |
|---|---------|---------|---------|
| 1 | `_fullFrameRate[i]` | CameraOutputSystem 选中该系统为直播输出 | 该系统 |
| 2 | `SubControlSystem.NeedRuning` | 当前激活的子系统要求满帧率（Inspector 配置） | 该系统 |
| 3 | `CameraOutputSystem.OnTransitionComplete()` | 显示切换动画完成 → SystemControl.DisplayChanged() | 全局重检 |
| 4 | SystemControl 缩略图开关 | 用户手动切换缩略图 Toggle | 全局重检 |
| 5 | 模块初始化完成 | 各 CAM 模块 `Start()` 末尾调用 `OnDeserialization()` | 首次全局启动 |

**判定公式**（每个 Camera 独立计算）：

```
needFullFrame[i] = _fullFrameRate[i] || cameras[i].currentSubsystem.NeedRuning;
```

#### 缩略图进入条件

Camera 进入缩略图模式需**同时满足**：

| 条件 | 说明 |
|------|------|
| `_fullFrameRate[i] == false` | 该 Camera 未被输出系统选中（不在直播画面中） |
| `currentSubsystem.NeedRuning == false` | 当前子系统不要求满帧率 |
| 上述所有退出信号均未置位 | 无任何满帧率触发条件 |

> 进入缩略图是**自动的**——当 `ThumbnailLoop()` 执行时，每个 Camera 根据自身条件独立判定。满帧率条件不满足时自动回退到缩略图模式，无需额外信号。

#### 重检触发统一入口

所有退出信号统一通过 SystemControl 的 `_RequestThumbnailRecheck()` 处理：

```
_RequestThumbnailRecheck():
  ├─ 取消当前 SendCustomEventDelayedSeconds("_ThumbnailLoop", ...)  ← 打断等待
  └─ 立即执行 _ThumbnailLoop()  ← 重新判定所有 Camera
```

| 触发源 | 调用路径 |
|--------|---------|
| CameraOutputSystem 切换完成 | `OnTransitionComplete()` → `_DisplayChanged(index)` → `_RequestThumbnailRecheck()` |
| 用户手动切换缩略图开关 | Toggle 回调 → `_RequestThumbnailRecheck()` |
| 初始化就绪 | 最后一个模块 `Start()` 完成后 → `systemControl.StartThumbnailLoop()`（首次启动，非重检） |

#### 缩略图执行时机

| 时机 | 行为 |
|------|------|
| **首次执行** | 所有 CAM 模块 `Start()` 调用自身 `OnDeserialization()` 完成后，SystemControl 启动 `ThumbnailLoop` |
| **手动触发** | `CameraOutputSystem.OnTransitionComplete()` / 缩略图开关变更 → 立即重检 |

#### 满帧率判定逻辑

```
VRCTDCamera[i] 的最终帧率模式由 SystemControl 中心判定:

  bool needFullFrame = _fullFrameRate[i]                    // 被输出系统选中（直播中）
                     || camera.currentSubsystem.NeedRuning; // 子系统要求满帧率
  
  camera.SetFullFrameRate(needFullFrame);
```

#### 边界情况

| 场景 | 判定 |
|------|------|
| 输出系统显示系统 2 | `_fullFrameRate[2]=true`，其余 `false`；系统2 满帧率，其余缩略图 |
| 切换到 TV 模式 | 全部 `_fullFrameRate=false`；NeedRuning 的子系统自保满帧率 |
| 无人机（NeedRuning=true）在系统3，输出显示系统1 | `_fullFrameRate[1]=true`，`_fullFrameRate[3]=false` 但 NeedRuning 兜底 → 系统1、3 均满帧率 |
| 所有系统 NeedRuning=false，当前无直播 | 全部进入缩略图循环 |
| 缩略图循环中发现某个系统 NeedRuning 变为 true | 立即退出缩略图 → 该系统满帧率 |

---

### 2.4 CameraOutputSystem — 显示输出管理（动画过渡版）

**同步模式**：`BehaviourSyncMode.Manual`  
**安装位置**：独立 GameObject  
**配套 Shader**：`Custom/TextureLerp`（`_MainTex` + `_SubTex` + `_Lerp`）

#### 职责

合并原 `DisPlayerM` + `FastCameraChanger`，升级为动画驱动画面切换：

1. **双纹理预览**：`_MainTex` = 当前投屏系统，`_SubTex` = 准备切换的目标系统
2. **动画过渡**：通过挂载的 Animator 驱动 Shader 的 `_Lerp` 实现渐变/切换效果
3. **批量初始化**：启动时将 RenderTexture 设置到多个静态显示器材质
4. **通知 SystemControl**：切换完成时调用 `SystemControl.DisplayChanged(currentIndex)`

#### Synced 同步变量

| 变量 | 类型 | 说明 |
|------|------|------|
| `currentTexIndex` | int | 当前正在显示的 RenderTexture 索引（`_MainTex`） |
| `nextTexIndex` | int | 准备切换到的 RenderTexture 索引（`_SubTex`） |
| `animationIndex` | int | 渐变动画类型（0=无动画/初始化，1+=具体动画样式） |

#### 纹理索引语义

`currentTexIndex` / `nextTexIndex` / `_pendingNextIndex` 使用**带符号索引**区分纹理来源：

| 索引范围 | 来源数组 | 映射公式 | 说明 |
|---------|---------|---------|------|
| `>= 0` | `cameraTextures[]` | `cameraTextures[index]` | VRCTDCamera 的 RenderTexture |
| `< 0` | `tvTextures[]` | `tvTextures[-index - 1]` | TV 画面源（-1 → tvTextures[0]，-2 → tvTextures[1]）|

代码统一通过 `_GetTexture(int index)` 和 `_IsValidIndex(int index)` 解析，不直接访问数组。

#### Inspector 引用

```
CameraOutputSystem Inspector:
  ├─ Material outputMaterial           ← 使用 Custom/TextureLerp 的材质
  ├─ RenderTexture[] cameraTextures    ← 各 VRCTDCamera 的 RenderTexture
  ├─ RenderTexture[] tvTextures        ← TV 画面源
  ├─ Animator transitionAnimator       ← 驱动切换动画的 Animator
  ├─ MeshRenderer[] staticDisplays     ← 批量静态显示器
  ├─ MeshRenderer[] tvDisplays         ← TV 显示器
  ├─ SystemControl systemControl       ← 通知切换完成
  └─ （内部状态）
      └─ int _pendingNextIndex         ← 等待动画完成的待切换索引（防止快速连续切换丢失目标）
```

#### Animator 约定

挂载在 CameraOutputSystem GameObject 上的 Animator 需要以下参数：

| 参数名 | 类型 | 说明 |
|--------|------|------|
| `TransitionType` | int | 渐变动画类型：0=初始化/重置，1+=具体动画索引 |
| `StartTransition` | bool (Toggle) | 触发开始渐变 |

Animator 中每个动画片段需在末尾添加 **Animation Event** 调用 `OnTransitionComplete()`。

#### 切换流程

```
用户触发切换（Dropdown / BottonIndex）:

1. CameraOutputSystem._SwitchTo(int newIndex, int animationType):
   │
   ├─ nextTexIndex = newIndex
   ├─ animationIndex = animationType
   │
   ├─ 2. 设置材质纹理:
   │   outputMaterial.SetTexture("_MainTex", _GetTexture(currentTexIndex))
   │   outputMaterial.SetTexture("_SubTex",  _GetTexture(nextTexIndex))
   │
   ├─ 3. 设置动画类型:
   │   transitionAnimator.SetInteger("TransitionType", animationIndex)
   │
   ├─ 4. 触发动画:
   │   transitionAnimator.SetBool("StartTransition", true)
   │
   ├─ SetOwner + RequestSerialization()  ← Manual 同步到所有客户端
   │
   └─ 5. 等待动画完成...

6. Animator Animation Event → OnTransitionComplete():
   │
   ├─ 7. 交换:
   │   currentTexIndex = nextTexIndex
   │   outputMaterial.SetTexture("_MainTex", _GetTexture(currentTexIndex))
   │
   ├─ 8. 重置 Animator:
   │   transitionAnimator.SetInteger("TransitionType", 0)
   │   transitionAnimator.SetBool("StartTransition", false)
   │
   ├─ 9. 通知 SystemControl:
   │   systemControl._DisplayChanged(currentTexIndex)
   │
   └─ RequestSerialization()  ← 同步最终状态
```

#### OnDeserialization 行为

```
OnDeserialization():
  │
  ├─ 如果 animationIndex == 0（无动画/已重置）:
  │   └─ 直接设置 _MainTex = cameraTextures[currentTexIndex]
  │       _SubTex 不需要设置（已切换完成）
  │
  ├─ 如果 animationIndex != 0（正在动画中）:
  │   ├─ 设置 _MainTex = cameraTextures[currentTexIndex]
  │   ├─ 设置 _SubTex  = cameraTextures[nextTexIndex]
  │   ├─ transitionAnimator.SetInteger("TransitionType", animationIndex)
  │   └─ transitionAnimator.SetBool("StartTransition", true)（等待本地 Animation Event）
```

#### 状态机

```
┌──────────┐     SwitchTo(n, type)     ┌──────────────┐
│  Idle    │ ─────────────────────────→ │ Transitioning│
│ (静止)    │                            │ (动画过渡中)  │
│          │ ←───────────────────────── │              │
└──────────┘   OnTransitionComplete()  └──────┬───────┘
                                               │
                                     OnDeserialization
                                     (animationIndex!=0)
                                               │
                                    其他客户端也进入 Transitioning
                                    等待各自的 Animation Event
```

#### 与 SystemControl 的关系

```
CameraOutputSystem
  ├─ 持有 SystemControl 直接引用
  │   ├─ 切换发起时 → systemControl._DisplayChanging(nextTexIndex)  （可选，提前通知）
  │   └─ 切换完成时 → systemControl._DisplayChanged(currentTexIndex)  （触发满帧率重检）
  │
  ├─ 不再直接操作 VRCTDCamera
  └─ SystemControl 通过读取 CameraOutputSystem.currentTexIndex 来判定满帧率
```

---

### 2.5 FovControlCAM + FovControlPlane — FOV 控制系统

**FovControlCAM**：安装于 CameraSystem  
**同步模式**：`BehaviourSyncMode.Continuous`

#### 三种模式

| 模式 | 行为 |
|------|------|
| 自动模式 | FOV 以速率持续变化：`FOV += -rate * multiplier * deltaTime` |
| 手动模式 | FOV 缓冲移动到目标值：`FOV += (target - FOV) * bufferValue * deltaTime` |
| 非 Owner | 插值跟踪同步值：`FOV += (syncedFOV - FOV) * deltaTime * compensation` |

#### isPlaneListening 回调守卫

CAM 侧持有 `public bool isPlaneListening` 标志，由 Plane 在切换 `cameraIndex` 时动态控制。
CAM 在以下时机回调 Plane 前检查此标志，仅当 `true` 时才通知 Plane 刷新 UI：

| 触发时机 | 回调方法 |
|---------|---------|
| `ApplyDefectFOV()` | `fovPlane._RefreshSlider(_targetFOV)` |
| `OnDeserialization()`（非 Owner） | `fovPlane._RefreshSlider(_fov)` |
| `OnOwnershipTransferred()`（新 Owner） | `fovPlane._RefreshSlider(_fov)` |

> **场景**：一个场景中只有一个 FovControlPlane 面板，同时只有一个 CAM 被监听。
> 非当前 `cameraIndex` 的 CAM 即使收到网络同步也不会干扰 UI 显示。

#### 接收子系统预设 FOV

```
FovControlCAM.ApplyDefectFOV(float defectFOV):
  写入 FOV 目标值（Clamp 到 [minFOV, maxFOV]）
  → 强制切换到手动模式（预设 FOV 作为目标值缓冲到达，不被自动模式速率覆盖）
  → [isPlaneListening] FovControlPlane 面板 Slider 同步更新
```

**调用链**：`SubControlSystem._ChangerTarget()` → `_camera.fovControlCAM.ApplyDefectFOV(fov)`（直接引用）

#### FovControlPlane — 多 CAM 切换

**同步模式**：`BehaviourSyncMode.None`（纯本地 UI）

```
FovControlPlane Inspector:
  ├─ SystemControl systemControl          ← 获取 VRCTDCamera[] 的入口
  ├─ Button cameraUpButton / cameraDownButton  ← 机位切换
  ├─ Text cameraIndexText                 ← 机位指示（如 "2/4"，可选）
  ├─ Slider fovSlider                     ← FOV 值调节
  ├─ Toggle autoModeToggle                ← 自动/手动模式
  ├─ Text fovValueText                    ← FOV 数值显示（可选）
  └─ （运行时构建）
      ├─ FovControlCAM[] fovCAMs          ← 从 systemControl.cameras[i].fovControlCAM 构建
      └─ int cameraIndex                  ← 当前选中索引 [0, fovCAMs.Length-1]
```

**初始化流程**：
```
Start():
  1. 从 SystemControl.cameras[] 读取 VRCTDCamera[]
  2. new FovControlCAM[count]
  3. for i in 0..count:
       fovCAMs[i] = cameras[i].fovControlCAM
       fovCAMs[i].fovPlane = this          ← 注入回引
  4. _ApplyCameraIndex()                  ← 设置 isPlaneListening + 刷新 UI
```

**cameraIndex 切换**（Up/Down Button → `_ApplyCameraIndex()`）：
```
1. 边界检查：cameraIndex = Clamp(cameraIndex, 0, fovCAMs.Length-1)，越界拒绝操作
2. 设置监听标志：for i: fovCAMs[i].isPlaneListening = (i == cameraIndex)
3. 从当前 CAM 刷新 UI：Slider.SetValueWithoutNotify / Toggle.SetIsOnWithoutNotify
4. 更新机位文本：cameraIndexText = "{cameraIndex+1}/{N}"
```

---

### 2.6 CameraControlCAM + CameraControlPlane — 位姿微调系统

**CameraControlCAM**：安装于 CameraSystem  
**同步模式**：`BehaviourSyncMode.Manual`

#### 职责

替代原 `CameraSpace` + `CameraSpaceControlSystem`：
- 6 轴微调（位置 XYZ + 旋转 XYZ）
- 所有权管理
- Manual 同步 Position/Rotation

#### 插值方案

采用**简单渐进式**（Linear Interpolation）：

```
Update():
  position = Vector3.MoveTowards(current, target, speed * deltaTime)
  rotation = Quaternion.RotateTowards(current, target, speed * deltaTime)
```

| 参数 | 说明 |
|------|------|
| `speed` | Inspector 配置的移动速度（通过 CameraControlPlane 的速度 Slider 动态调整） |
| 到达判定 | `\|current - target\| < 0.01` 时视为已到达，停止插值 |

到达判定阈值 `0.01` 同时适用于位置（距离差）和旋转（角度差，通过 `Quaternion.Angle` 判定）。

#### 序列化冷却

Manual 同步采用 0.5s 冷却防抖动：`_TrySerialize()` → 冷却内仅标记 `_pendingSerialization` → `SendCustomEventDelayedSeconds("_FlushPendingSerialization", delay)` 延迟发送最终值。避免拖拽 Slider 时每帧 `RequestSerialization`。

#### isPlaneListening 回调守卫

CAM 侧持有 `public bool isPlaneListening` 标志，由 Plane 在切换 `cameraIndex` 时动态控制。
CAM 在以下时机回调 Plane 前检查此标志：

| 触发时机 | 回调方法 |
|---------|---------|
| `ResetToCurrent()` | `controlPlane._RefreshAllSliders(pos, euler)` |
| `OnDeserialization()`（非 Owner） | `controlPlane._RefreshAllSliders(pos, euler)` |

> 非 Owner 在 `OnDeserialization` 中强制 `_isMoving = true`，依赖 `Update()` 到达判定自然收敛。
> 即使 Owner 发送的是"停止"（目标 = 当前位置），也会通过一帧 Update 后到达并自动停止。

#### CameraControlPlane — 多 CAM 切换

**同步模式**：`BehaviourSyncMode.None`（纯本地 UI）

```
CameraControlPlane Inspector:
  ├─ SystemControl systemControl          ← 获取 VRCTDCamera[] 的入口
  ├─ Button cameraUpButton / cameraDownButton  ← 机位切换
  ├─ Text cameraIndexText                 ← 机位指示（如 "2/4"，可选）
  ├─ Slider posX/Y/Z + rotX/Y/Z          ← 6 轴位姿
  ├─ Slider speedSlider                   ← 移动速度
  ├─ Button resetButton / stopButton      ← 重置 / 停止
  └─ （运行时构建）
      ├─ CameraControlCAM[] camArray      ← 从 systemControl.cameras[i].cameraControlCAM 构建
      └─ int cameraIndex                  ← 当前选中索引 [0, camArray.Length-1]
```

**初始化与切换流程**：与 FovControlPlane 相同模式（`Start()` 构建数组 → 注入回引 → `_ApplyCameraIndex()` 设置 `isPlaneListening` + 刷新 UI）。

---

### 2.7 TrackingPlayerCAM + TrackingPlayerPlane — 玩家跟踪系统

**TrackingPlayerCAM**：安装于 CameraSystem  
**同步模式**：`BehaviourSyncMode.Manual`，使用 `[NetworkCallable]`

> `isPlaneListening` 回调守卫已实现（与 FovControlCAM / CameraControlCAM 一致）。
> `trackingPlane` 回引已存在。

**TrackingPlayerPlane**：集成原 `PlayerTrackingControl` + `QuickNameChoose`

> 多 CAM 切换已实现（与 FovControlPlane / CameraControlPlane 一致模式）。
> Non-TempWrite 模式下 UI 直接写入为**设计意图**（仅本地预览），同步由 `_ApplyPreview` 统一触发。

#### 功能集成

| 原系统 | 整合至 |
|--------|--------|
| `PlayerTrackingControl` | TrackingPlayerPlane 主 UI |
| `QuickNameChoose` | TrackingPlayerPlane 内置玩家名选择区域 |

#### 跟踪目标切换（参考旧系统 PlayerTrackingSystem）

```
Update() — 每帧跟踪逻辑:
  if (!_trackingActive) return;

  VRCPlayerApi.TrackingData data = trackingTarget.GetTrackingData(trackingDataType);

  if (useRelativePosition):
    // 相对模式：世界 TrackingData → 本地空间（相对父点位）
    // 位置：世界空间 data.position → InverseTransformPoint 转本地空间
    trackingTransform.localPosition = parent.InverseTransformPoint(data.position);

    // 旋转：同步更新部位旋转值（从 TrackingData 获取）
    if (rotationOffset):
      // 旋转反转
      Quaternion worldRot = Quaternion.Inverse(data.rotation);
      trackingTransform.localRotation = Quaternion.Inverse(parentWorldRot) * worldRot;
    else:
      // 应用旋转锁定（逐轴归零）
      Vector3 euler = data.rotation.eulerAngles;
      if (rotationLock[0]) euler.x = 0;
      if (rotationLock[1]) euler.y = 0;
      if (rotationLock[2]) euler.z = 0;
      Quaternion worldRot = Quaternion.Euler(euler);
      trackingTransform.localRotation = Quaternion.Inverse(parentWorldRot) * worldRot;

  else:
    // 绝对模式：位置 = 本地偏移，旋转归零（不读取 TrackingData 旋转值）
    trackingTransform.localPosition = positionOffset;
    trackingTransform.localRotation = Quaternion.identity;
```

#### 所有权变更

```
OnOwnershipTransferred(VRCPlayerApi player):
  trackingTarget = player;  // 跟踪目标变为新的 Owner
  // playerID 由 VRCTDCamera 同步，反序列化时调用本方法
```

#### 跟踪类型

| TrackingID | 部位 | TrackingDataType |
|------------|------|-----------------|
| 0 | 头部 (Head) | Head |
| 1 | 左手 (LeftHand) | LeftHand |
| 2 | 右手 (RightHand) | RightHand |
| 3 | 模型原点 (Origin) | Origin |
| 其他 | 关闭跟踪 | — |

#### 同步参数

| 参数 | 类型 | 同步方式 | 额外变量（预览） | 说明 |
|------|------|---------|-----------------|------|
| `trackingID` | int | NetworkCallable | `_trackingIDPreview` | 跟踪类型 |
| `positionOffset` | Vector3 | NetworkCallable | `_positionOffsetPreview` | 位置偏移 XYZ |
| `useRelativePosition` | bool | NetworkCallable | `_useRelativePositionPreview` | 相对/绝对模式 |
| `rotationOffset` | bool | NetworkCallable | `_rotationOffsetPreview` | 旋转反转 |
| `rotationLock[3]` | bool[] | NetworkCallable | `_rotationLockPreview` | 各轴旋转锁定 |
| `appearance` | bool | NetworkCallable | `_appearancePreview` | 跟踪指示器显示 |

每个参数通过 `[NetworkCallable]` → `_ScheduleSync()`（延迟 1 秒合并） → `_DoSync()`（增量比对，仅发送变更值） → `RequestSerialization()` 流程同步。

#### 临时写入模式

```
TempWrite Toggle（TrackingPlayerPlane 上）:
  ├─ true:  所有 Slider/Toggle 写入额外预览变量（_xxxPreview），仅本地生效
  └─ false: 写入 [UdonSynced] 变量 → 走 NetworkCallable 标准同步流程
```

#### 玩家名快速选择（集成 QuickNameChoose）

合并原 `QuickNameChoose` 到 TrackingPlayerPlane 内部，**直接发送到 TrackingPlayerCAM，不再通过 SystemControl 中转**。

```
TrackingPlayerPlane 内置玩家名选择区域:

  Display() — 刷新玩家列表:
    ├─ VRCPlayerApi.GetPlayers(allPlayers)
    ├─ 提取各玩家 displayName → Displayers[]
    ├─ 动态实例化按钮（每个按钮 = 一个可选玩家）
    │   按钮文本: "#0 PlayerName", "#1 PlayerName2", ...
    └─ 每个按钮点击 → SetToken(index)

  SetToken(int index):
    ├─ 获取 Displayers[index] 对应的 VRCPlayerApi
    ├─ int playerID = targetPlayer.playerId
    └─ 直接写入 TrackingPlayerCAM:
         trackingCAM.SetTrackingTarget(playerID)
         // ↑ 直接引用调用，不经 SystemControl

TrackingPlayerCAM.SetTrackingTarget(int id):
  ├─ VRCPlayerApi target = VRCPlayerApi.GetPlayerById(id)
  ├─ if (target == null || !target.IsValid()):
  │   └─ 目标无效 → 不做任何操作（保持当前跟踪目标）
  ├─ 如果目标有效 → SetOwner(target, this.gameObject) → 所有权转移
  └─ trackingID → NetworkCallable 同步
```

**与原系统的区别**：

| 方面 | 原系统 | 新系统 |
|------|--------|--------|
| 玩家列表触发 | QuickNameChoose.Display() → Main.SendCustomEvent("ResetDisplay") | TrackingPlayerPlane.Display() 直接刷新 |
| 目标下发路径 | QuickNameChoose → Main.SetProgramVariable("DisPlayName") → 发送控制信号.InteractStart() → ControlCenter.StartChanger() → DisplayChanger() | TrackingPlayerPlane → TrackingPlayerCAM.SetTrackingTarget(playerID) |
| 跨脚本调用次数 | 4 层 | **1 层**（直接引用） |

---

## 3. 通信规则

### 3.1 通信方式矩阵

```
┌────────────────────┬──────────────┬──────────────────────────┐
│ 通信方向            │ 方式         │ 示例                     │
├────────────────────┼──────────────┼──────────────────────────┤
│ Plane → CAM        │ 直接引用调用  │ SystemControl → VRCTDCamera.StartChanger() │
│ CAM → CAM          │ 直接引用      │ VRCTDCamera → FovControlCAM.ApplyDefectFOV()│
│ Plane → Plane      │ 直接引用      │ SystemControl → TrackingPlayerPlane        │
│ 独立系统 → Plane   │ 直接引用      │ CameraOutputSystem → SystemControl.DisplayChanged()│
│ 子系统 → 点位附Udon │ SetProgramVariable / SendCustomEvent │ SubControlSystem → 点位 "_OnPointActivated" │
| 点位附Udon → 子系统 | SetProgramVariable | 点位 "SlarpV" → SubControlSystem |
└────────────────────┴──────────────┴──────────────────────────┘
```

### 3.2 SetProgramVariable 保留范围

**仅在**以下场景使用 `SetProgramVariable`/`GetProgramVariable`/`SendCustomEvent`：

1. `SubControlSystem.Start()` → 点位附Udon：`SetProgramVariable("Managerudon", this)`（NeedCallBack 控制）
2. `SubControlSystem._ChangerTarget()` → 点位：`SendCustomEvent("_OnPointActivated")` + `SetProgramVariable("VoidObjectActive", ...)`（SpecialSignal）
3. `SubControlSystem._ChangerTarget()` → 点位：`GetProgramVariable("FOV")`（FOVDefectUse）
4. 点位附Udon → 子系统：`SetProgramVariable("SlarpV", ...)`（通过 Managerudon 回调）

其余所有通信均使用 C# 直接引用调用。

### 3.3 带参网络同步规范

使用带参数的 `NetworkCallable` 网络事件时，必须满足以下条件：

| 条件 | 说明 |
|------|------|
| **① using 语句** | 文件头部必须包含 `using VRC.SDK3.UdonNetworkCalling;` |
| **② 调用方式** | 必须使用 `NetworkCalling.SendCustomNetworkEvent`（带参版本），不可使用 `SendCustomNetworkEvent` |
| **③ 目标指定** | 强制转换为 `(IUdonEventReceiver)target`，等效 `udon.SendCustomNetworkEvent` 的 `udon` 部分 |

**完整语法**：
```csharp
using VRC.SDK3.UdonNetworkCalling;

NetworkCalling.SendCustomNetworkEvent(
    (IUdonEventReceiver)targetScript,    // 目标 UdonBehaviour（强制 IUdonEventReceiver）
    NetworkEventTarget.All,              // 发送目标：All / Owner
    "SetTrackingDataType",               // 方法名
    param0, param1, param2...            // 参数（白名单类型：int/float/bool/Vector3/string）
);
```

**应用范围**：系统中仅 `TrackingPlayerCAM` 使用此机制（用于追踪参数的实时同步）。

### 3.4 事件命名规范

所有自定义事件（`SendCustomEvent` / `SendCustomNetworkEvent` 的目标方法）遵循以下命名规则：

| 事件类型 | 命名规则 | 示例 |
|---------|---------|------|
| VRC API 事件 | 保持原名（不可更改） | `OnDeserialization`, `OnPlayerJoined`, `OnPickupUseDown` |
| Unity 事件 | 保持原名（不可更改） | `Start`, `Update`, `OnEnable`, `OnDisable` |
| 网络同步事件 | `[NetworkCallable]` + 清晰描述 | `SetTrackingDataType`, `SetPositionOffset` |
| **非网络同步的自定义事件** | **前缀 `_`** | `_ChangerTarget`, `_RefreshRenderFrame`, `_ThumbnailLoop` |

> **规则**：除了 VRC API 事件和 Unity 生命周期事件外，所有非网络同步的自定义事件方法名前加 `_`，从命名上明确区分"这个方法不会通过网络传播"。

### 3.5 子系统 ↔ 点位通信标准

子系统与点位之间的通信是系统中**唯一**保留 `SetProgramVariable` / `GetProgramVariable` / `SendCustomEvent` 的路径。以下为双方必须遵守的变量和事件约定。

#### 子系统 → 点位

| 操作 | 变量/事件 | 触发条件 | 说明 |
|------|----------|---------|------|
| 注册回调 | `SetProgramVariable("Managerudon", this)` | `NeedCallBack = true`，子系统 `Start()` 时 | 将子系统的 UdonBehaviour 引用写入点位，点位据此反向通信 |
| 启动点位附Udon | `SendCustomEvent("_OnPointActivated")` | SpecialSignal，`_ChangerTarget()` 时 | 通知点位附Udon 开始运行（此时 VoidObjectActive 已写入） |
| 传递激活状态 | `SetProgramVariable("VoidObjectActive", value)` | SpecialSignal，`_ChangerTarget()` 时 | 传递当前 VoidObjectActive 值 |
| 读取预设 FOV | `GetProgramVariable("FOV")` → float | FOVDefectUse，`_ChangerTarget()` 时 | 从点位读取预设 FOV 值 |

> **约定**：点位附Udon 必须暴露以下公共变量以接收子系统写入：
> - `UdonBehaviour Managerudon` — 接受子系统引用
> - `bool VoidObjectActive` — 接受激活状态
> - （可选）`float FOV` — 提供预设 FOV 值

#### 点位 → 子系统

| 操作 | 变量 | 触发条件 | 说明 |
|------|------|---------|------|
| 修改缓动速度 | `SetProgramVariable("SlarpV", floatValue)` | 点位 UI Slider 值变更时 | 通过 `Managerudon` 引用回调子系统，修改其缓动插值速度 |

> **约定**：子系统必须暴露 `float SlarpV` 公共变量以接受点位回调写入。

#### 通信时序

```
子系统激活流程:
  Start()
    ├─ [NeedCallBack] → 点位.SetProgramVariable("Managerudon", this)
    │                    点位获得子系统引用
    └─ （等待 _ChangerTarget 被调用）

  _ChangerTarget():
    ├─ 激活目标点位 GameObject
    ├─ [SpecialSignal] → 点位.SendCustomEvent("_OnPointActivated")
    │                  → 点位.SetProgramVariable("VoidObjectActive", value)
    │                    点位附Udon 启动并读取当前参数
    ├─ [FOVDefectUse]  → float fov = 点位.GetProgramVariable("FOV")
    │                  → _camera.fovControlCAM.ApplyDefectFOV(fov)
    └─ 子系统进入运行状态

运行时回调:
  点位 UI 操作（如 HandTracking Slider 拖动）
    → Managerudon.SetProgramVariable("SlarpV", newValue)
      → 子系统.SlarpV 更新
        → 子系统 Update() 中的缓动计算使用新的 SlarpV
```

#### 点位 GameObject 结构约定

```
点位根 GameObject
├── [点位核心逻辑]  ← 可选的 Transform 锚点
├── [点位附Udon]     ← 附加的 UdonBehaviour（SpecialSignal/NeedCallBack/FOVDefectUse 操作的对象）
│   必须暴露的公共变量:
│   ├─ UdonBehaviour Managerudon
│   ├─ bool VoidObjectActive
│   └─ float FOV（可选，FOVDefectUse=true 时必须）
│   必须响应的自定义事件:
│   └─ _OnPointActivated()
└── （其他美术/功能子节点）
```

---

## 4. 初始化流程

### 4.1 初始化顺序

```
场景加载
  │
  ├─ [帧0] 所有脚本 Start()
  │   ├─ VRCTDCamera[0..N].Start()
  │   │   └─ 收集组件引用 → 调用 OnDeserialization() 应用初始同步状态
  │   ├─ SubControlSystem[0..N].Start()
  │   │   └─ 如果 NeedCallBack → 注册到点位附Udon
  │   ├─ CameraOutputSystem.Start()
  │   │   └─ 设置初始纹理 → 调用 OnDeserialization()
  │   ├─ FovControlCAM.Start()
  │   │   └─ 调用 OnDeserialization() 应用初始同步状态
  │   ├─ CameraControlCAM.Start()
  │   │   └─ 调用 OnDeserialization() 应用初始同步状态
  │   ├─ TrackingPlayerCAM.Start()
  │   │   └─ 调用 OnDeserialization() 应用初始同步状态
  │   ├─ SystemControl.Start()
  │   │   ├─ 计算 _thumbnailDelay = 1f / thumbnailRefreshRate
  │   │   ├─ 等待所有 CAM 模块就绪
  │   │   └─ （暂不启动缩略图循环——等待首次统一的就绪信号）
  │   └─ 各 Plane.Start()
  │       └─ 调用对应 CAM 的 OnDeserialization()
  │
  └─ [帧1+] 所有模块完成自身初始化并调用 OnDeserialization 后
      → SystemControl 收到就绪信号 → 启动缩略图循环
```

#### 初始化约定

**所有 CAM 模块在 Start() 末尾必须手动调用自身的 `OnDeserialization()`**，确保网络同步状态在初始化阶段被正确应用。同样，在以下时机也需要触发：

| 时机 | 触发方式 | 说明 |
|------|---------|------|
| 模块 Start() 完成 | 调用自身 `OnDeserialization()` | 应用初始同步状态 |
| 反序列化触发 | 自动回调 `OnDeserialization()` | 同步变量变更 |
| 手动触发（外部调用） | 其他模块调用 `SendCustomEvent("OnDeserialization")` | 如 SystemControl 触发的缩略图重检 |

**缩略图循环启动条件**：所有模块完成初始化并调用 `OnDeserialization` 后，SystemControl 才启动 `ThumbnailLoop`。在此之前不渲染缩略图。

### 4.2 与原系统对比

| 方面 | 原系统 | 新系统 |
|------|--------|--------|
| 引用获取 | `parent.Find("CameraTranform")` 字符串路径 | Inspector 直接引用 |
| 跨脚本通信 | `GetProgramVariable("CameraRender")` 反射 | 直接引用调用 |
| 初始化顺序 | 多层 `SendCustomEventDelayedFrames` 链 | 最小化延迟，Inspector 引用无需等待 |
| 状态管理 | 分散在各脚本的 bool 变量 | VRCTDCamera 集中管理 + SystemControl 中心决策 |

---

## 5. 暂未纳入的模块

| 模块 | 原系统对应 | 处理方式 |
|------|-----------|---------|
| 点位系统 | HandTracking / FlyCameraSystem / 相对追踪 | 现阶段不设计，由你后续处理 |
| 预设系统 | DefaultJSON / FastSaveOFF / PresetKeyBoard | 待后续作为 `辅助面板/PresetPlane` |
| 动画器控制 | AnimatorControl / AnimatorFastSYNC | 不属于相机核心系统，独立为后续模块 |
| CameraDataSYNC | 30 行 FOV 同步 | 已被 FovControlCAM 的 Continuous 同步覆盖 |
| BottonIndex / GetContext / TeleportGameObject | 辅助 UI 按钮 | 按需在 Plane 中内联 |

---

## 6. 文件夹结构

```
Assets/RhineLab/VRChatFansTDsystem/
├── DOCS/
│   ├── SYSTEM_ANALYSIS.md          ← 原系统架构分析
│   ├── INIT_FLOW_ANALYSIS.md       ← 初始化流程分析
│   ├── MODULE_BREAKDOWN.md         ← 四模块分项分析
│   └── SYSTEM_DESIGN.md            ← 本文档（设计方案）
├── Script/
│   ├── 图像输出系统/
│   │   └── CameraOutputSystem.cs   ← 显示输出管理
│   ├── 点位系统/                   ← （暂空，待后续）
│   ├── 相机控制系统/
│   │   ├── FovControlCAM.cs        ← FOV 执行
│   │   ├── FovControlPlane.cs      ← FOV 控制面板
│   │   ├── CameraControlCAM.cs     ← 位姿微调执行
│   │   └── CameraControlPlane.cs   ← 位姿控制面板
│   ├── 相机控制面板/
│   │   ├── SystemControl.cs        ← 导播主面板
│   │   └── 辅助面板/               ← （暂空，待预设系统）
│   ├── 相机系统/
│   │   ├── VRCTDCamera.cs          ← 单相机核心
│   │   └── SubControlSystem.cs     ← 点位子系统基类
│   └── 跟踪控制系统/
│       ├── TrackingPlayerCAM.cs    ← 跟踪执行
│       └── TrackingPlayerPlane.cs  ← 跟踪面板 + 玩家名选择
├── Simple.unity                    ← 场景
└── （其他资源文件夹）
```

---

> 📝 本文档为 VRChatFansTDsystem 的完整设计方案，作为编码工作的唯一技术基准。任何设计变更需先更新此文档。
