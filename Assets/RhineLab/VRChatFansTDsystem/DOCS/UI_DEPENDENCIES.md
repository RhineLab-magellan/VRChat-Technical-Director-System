# VRChat Fans TD System — UI 依赖清单

> 生成日期: 2026-06-10  
> 格式: `Unity UI 类型` — `字段名` — `作用说明`

---

## 一、SystemControl（导播控制面板）

| UI 类型 | 字段名 | 作用 |
|----------|--------|------|
| `Toggle` | `thumbnailToggle` | 缩略图功能全局开关，On→启动缩略图循环，Off→停止循环并关闭所有 Camera |

---

## 二、FovControlPlane（FOV 控制面板）

| UI 类型 | 字段名 | 作用 |
|----------|--------|------|
| `Button` | `cameraUpButton` | 机位 +1 切换（cameraIndex++），切换到下一个 CAM 的 FOV 控制 |
| `Button` | `cameraDownButton` | 机位 -1 切换（cameraIndex--），切换到上一个 CAM 的 FOV 控制 |
| `Text` | `cameraIndexText` | 机位指示文本，显示 `"当前机位/总机位数"`（如 `"1/4"`） |
| `Slider` | `fovSlider` | FOV 值拖动条，范围需与 `FovControlCAM.minFOV/maxFOV` 一致；自动模式下不可交互 |
| `Toggle` | `autoModeToggle` | 自动/手动模式切换，On=自动（速率持续变化），Off=手动（缓冲到目标值） |
| `Text` | `fovValueText` | 当前 FOV 数值文本，格式 `"60.0°"` |

---

## 三、CameraControlPlane（相机位姿控制面板）

| UI 类型 | 字段名 | 作用 |
|----------|--------|------|
| `Button` | `cameraUpButton` | 机位 +1 切换（cameraIndex++），切换到下一个 CAM 的位姿控制 |
| `Button` | `cameraDownButton` | 机位 -1 切换（cameraIndex--），切换到上一个 CAM 的位姿控制 |
| `Text` | `cameraIndexText` | 机位指示文本，显示 `"当前机位/总机位数"`（如 `"1/4"`） |
| `Slider` | `posXSlider` | 相机 localPosition X 轴微调 Slider |
| `Slider` | `posYSlider` | 相机 localPosition Y 轴微调 Slider |
| `Slider` | `posZSlider` | 相机 localPosition Z 轴微调 Slider |
| `Slider` | `rotXSlider` | 相机 localRotation Euler X 轴微调 Slider（0~360°） |
| `Slider` | `rotYSlider` | 相机 localRotation Euler Y 轴微调 Slider |
| `Slider` | `rotZSlider` | 相机 localRotation Euler Z 轴微调 Slider |
| `Slider` | `speedSlider` | 位姿插值移动速度 Slider，值越大过渡越快 |
| `Button` | `resetButton` | 重置按钮，将目标位姿重置为当前 Transform 实际位姿，停止插值 |
| `Button` | `stopButton` | 暂停按钮，立即停止位姿插值（以当前位置作为新目标） |

---

## 四、TrackingPlayerPlane（玩家跟踪控制面板）

| UI 类型 | 字段名 | 作用 |
|----------|--------|------|
| `Button` | `cameraUpButton` | 机位 +1 切换（cameraIndex++），切换到下一个 CAM 的跟踪控制 |
| `Button` | `cameraDownButton` | 机位 -1 切换（cameraIndex--），切换到上一个 CAM 的跟踪控制 |
| `Text` | `cameraIndexText` | 机位指示文本，显示 `"当前机位/总机位数"`（如 `"1/4"`） |
| `Dropdown` | `trackingTypeDropdown` | 跟踪类型下拉：0=Head, 1=LeftHand, 2=RightHand, 3=Origin |
| `Slider` | `offsetXSlider` | 跟踪位置偏移 X 轴 Slider（绝对模式下叠加到 TrackingData.position） |
| `Slider` | `offsetYSlider` | 跟踪位置偏移 Y 轴 Slider |
| `Slider` | `offsetZSlider` | 跟踪位置偏移 Z 轴 Slider |
| `Toggle` | `relativeModeToggle` | 相对/绝对模式：On=完全跟随 TrackingData，Off=位置偏移+旋转归零 |
| `Toggle` | `rotationOffsetToggle` | 旋转反转：On=Quaternion.Inverse(data.rotation)，仅在相对模式下生效 |
| `Toggle` | `lockXToggle` | X 轴旋转锁定：On=该轴欧拉角归零 |
| `Toggle` | `lockYToggle` | Y 轴旋转锁定：On=该轴欧拉角归零 |
| `Toggle` | `lockZToggle` | Z 轴旋转锁定：On=该轴欧拉角归零 |
| `Toggle` | `indicatorToggle` | 跟踪指示器 MeshRenderer 显示/隐藏 |
| `Toggle` | `tempWriteToggle` | TempWrite 预览模式开关：On=所有修改写入预览变量仅本地生效；Off=退出预览 |
| `Button` | `applyPreviewButton` | 应用预览按钮，将 TempWrite 预览变量提交到工作变量并触发网络同步 |
| `Button` | `cancelPreviewButton` | 取消预览按钮，放弃 TempWrite 预览，恢复为当前工作值 |
| `Button` | `refreshPlayersButton` | 刷新在线玩家列表按钮，重新生成玩家名选择按钮 |
| `Transform` | `playerButtonContainer` | 玩家名按钮父容器 Transform（动态生成的按钮放置于此） |
| `GameObject` | `playerButtonPrefab` | 玩家名按钮预制体（需带 Text 子节点 + Button 组件 + UdonBehaviour） |

---

## 五、CameraOutputSystem（显示输出管理系统）

> 无直接 `UnityEngine.UI` 依赖。画面切换通过 `Animator` 动画驱动 `Custom/TextureLerp` Shader 的 `_Lerp` 参数实现渐变。

---

## 六、CAM 执行脚本（无 UI 依赖）

以下脚本为纯执行层，不含 `UnityEngine.UI` 字段：

| 脚本 | 说明 |
|------|------|
| `VRCTDCamera` | 单相机实例注册中心 + Manual 网络同步，持有所有子系统的 Inspector 引用 |
| `SubControlSystem` | 点位子系统基类，管理点位切换与四标志协同逻辑 |
| `PointDefect` | 点位附 Udon 标准模板，与 SubControlSystem 通过 `SetProgramVariable`/`SendCustomEvent` 通信 |
| `FovControlCAM` | FOV 执行系统 — Continuous 网络同步，三模式（自动/手动/非Owner插值），含 `isPlaneListening` 回调守卫 |
| `CameraControlCAM` | 相机位姿微调执行 — Manual 同步，6 轴 MoveTowards/RotateTowards 插值，0.5s 序列化冷却，含 `isPlaneListening` 回调守卫 |
| `TrackingPlayerCAM` | 玩家跟踪执行 — Manual 同步 + [NetworkCallable] 带参网络事件，支持相对/绝对模式，localPosition/localRotation 本地空间，含 `isPlaneListening` 回调守卫 |

---

## 汇总统计

| UI 类型 | 出现次数 | 分布脚本 |
|----------|----------|----------|
| `Button` | 11 | FovControlPlane(2), CameraControlPlane(4), TrackingPlayerPlane(5) |
| `Slider` | 11 | FovControlPlane(1), CameraControlPlane(7), TrackingPlayerPlane(3) |
| `Toggle` | 9 | SystemControl(1), FovControlPlane(1), TrackingPlayerPlane(7) |
| `Text` | 4 | FovControlPlane(2), CameraControlPlane(1), TrackingPlayerPlane(1) |
| `Dropdown` | 1 | TrackingPlayerPlane(1) |
| `Transform` | 1 | TrackingPlayerPlane(1) — playerButtonContainer |
| `GameObject` | 1 | TrackingPlayerPlane(1) — playerButtonPrefab |
| **总计** | **38** | |

---

## Plane ↔ CAM 回调关系图

```
┌─────────────────────────────────────────────────────────────┐
│                     SystemControl                           │
│  Toggle thumbnailToggle ──→ StartThumbnailLoop()/Stop...   │
│  (通过 _DispatchStartChanger 下发参数到 VRCTDCamera)        │
└──────────┬──────────────────────────┬──────────────────────┘
           │ cameras[]                │ outputSystem
           ▼                          ▼
┌──────────────────┐    ┌─────────────────────────────┐
│   VRCTDCamera    │    │   CameraOutputSystem         │
│  (无UI依赖)       │    │   (无UI依赖, Animator驱动)    │
└──┬───┬───┬──────┘    └─────────────────────────────┘
   │   │   │
   ▼   ▼   ▼
┌──────────┐ ┌──────────────┐ ┌──────────────────┐
│FovControl│ │CameraControl │ │TrackingPlayer    │
│   CAM    │ │    CAM       │ │     CAM          │
│(无UI依赖) │ │  (无UI依赖)   │ │   (无UI依赖)      │
└──┬───────┘ └──┬───────────┘ └──┬───────────────┘
   │fovPlane    │controlPlane    │trackingPlane
   ▼            ▼                ▼
┌──────────┐ ┌──────────────┐ ┌──────────────────┐
│FovControl│ │CameraControl │ │TrackingPlayer    │
│  Plane   │ │   Plane      │ │    Plane         │
│ (6 UI)   │ │  (12 UI)     │ │   (18 UI)        │
└──────────┘ └──────────────┘ └──────────────────┘
```

- **Plane** 通过 `cameraIndex` 切换当前控制的 CAM，设置 `isPlaneListening` 标志
- **CAM** 在网络同步/所有权转移时，通过 `isPlaneListening` 判断是否回调 Plane 刷新 UI
- Plane 使用 `SetXxxWithoutNotify` 刷新 UI 避免循环触发 `OnValueChanged`

---

## Inspector 绑定要点

| 绑定位置 | 事件 | 绑定方法 |
|----------|------|----------|
| FOV Slider | `OnValueChanged` | `FovControlPlane._OnFovSliderChanged` |
| 自动模式 Toggle | `OnValueChanged` | `FovControlPlane._OnAutoModeToggleChanged` |
| 缩略图 Toggle | `OnValueChanged` | `SystemControl._OnThumbnailToggleChanged` |
| 位姿 Slider（6轴） | `OnValueChanged` | `CameraControlPlane._OnAnyPoseSliderChanged` |
| 速度 Slider | `OnValueChanged` | `CameraControlPlane._OnSpeedSliderChanged` |
| 机位 Up/Down 按钮 | `OnClick` | 各自 Plane 的 `_OnCameraUpClicked` / `_OnCameraDownClicked` |
| 重置/停止按钮 | `OnClick` | `_OnResetClicked` / `_OnStopClicked` |
| 跟踪所有 UI | `OnValueChanged` | 对应 `_OnXxxChanged` 方法 |
| 跟踪 Dropdown | `OnValueChanged` | `_OnTrackingTypeChanged` |
| TempWrite/应用/取消 | `OnClick` / `OnValueChanged` | 对应 `_OnXxxClicked` / `_OnXxxChanged` |
| 刷新玩家列表 | `OnClick` | `_OnRefreshPlayersClicked` |
| 播放器按钮 Prefab | UdonBehaviour 编程绑定 | `SetProgramVariable("targetPlane")` + `SetProgramVariable("playerIndex")` |

---

## 注意事项

1. **UdonSharp 约束**：所有 `public` UI 字段必须在 Unity Inspector 中手动拖拽绑定，不支持 `[SerializeField]` private 字段的自动解析。
2. **循环调用防护**：Plane 刷新 UI 时必须使用 `SetValueWithoutNotify` / `SetIsOnWithoutNotify`，否则会触发 `OnValueChanged` 导致无限循环。
3. **isPlaneListening 机制**：CAM 脚本只在 `isPlaneListening=true` 时才回调 Plane 刷新 UI，防止多个 Plane 实例同时监听同一 CAM 导致混乱。
4. **TempWrite 模式**：`TrackingPlayerPlane` 的 TempWrite 机制允许用户在本地预览参数修改，确认后一次性提交，避免每次 Slider 拖动都触发网络同步。
5. **玩家按钮动态生成**：`TrackingPlayerPlane` 通过 `Instantiate(playerButtonPrefab)` 动态创建玩家选择按钮，按钮上的 UdonBehaviour 通过 `SetProgramVariable` 注入 `targetPlane` 和 `playerIndex` 实现回调。
