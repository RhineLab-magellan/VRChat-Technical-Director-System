# 网络同步审查 — 待处理事项

> 📅 审查日期：2026-06-09  
> 📝 记录 CameraControlCAM / FovControlCAM / FovControlPlane / CameraControlPlane 审查中发现的遗留设计问题

---

## 1. CameraControlCAM — 子系统切换时目标位姿不同步

**结论：不是问题。** ❌ 已排除。

**架构说明**：
- 点位 A（子系统控制）是场景中的相机锚点 GameObject，负责粗定位
- `cameraTransform` 是 A 的子节点，CameraControlCAM 控制其 **localPosition / localRotation**（相对 A 的偏移）
- 子系统切换时 A 移动，但 CameraControlCAM 的本地目标不变——子节点自动跟随父节点移动，本地偏移保持

**代码验证**（`CameraControlCAM.cs`）：
```csharp
// Start() — 初始目标取自本地空间
_targetPosition = controlTransform.localPosition;   // 第 97 行
_targetRotation = controlTransform.localRotation;   // 第 98 行

// Update() — 插值计算也在本地空间
Vector3 currentPos = controlTransform.localPosition;  // 第 112 行
Quaternion currentRot = controlTransform.localRotation; // 第 113 行
```

✅ 无需修复。

---

## 2. TrackingPlayerCAM — 需统一为本地空间（localPosition/localRotation）

**结论：已修复。** ✅

**修复内容**（`TrackingPlayerCAM.cs` Update()）：
- `trackingTransform.position` → `trackingTransform.localPosition`
- `trackingTransform.rotation` → `trackingTransform.localRotation`
- 世界 TrackingData 通过 `parent.InverseTransformPoint()` 和 `Quaternion.Inverse(parentRot) * worldRot` 转换为本地空间
- 无父节点时回退为世界空间（`localPosition = data.position`）

---

## 5. TrackingPlayerCAM — Late Joiner 参数同步

**澄清：不影响。** ℹ️ 按设计流程运作。

**设计流程**：
```
导播(任意客户端) → NetworkCallable 带参事件 → 所有客户端(含 Owner) 收到并更新数据
  → Owner 调用 RequestSerialization() → [UdonSynced] _trackingID 同步到 Late Joiner
```

Late Joiner 通过 `OnDeserialization` 恢复 `_trackingID`，其余 5 个参数（`positionOffset` 等）通过 NetworkCallable 事件在运行中持续同步。Late Joiner 初始使用 Inspector 默认值，后续事件到达后更新。

---

## 6. TrackingPlayerCAM + Plane — 所有权模型

**澄清：按设计流程运作。** ℹ️

**设计流程**：
```
导播(任意客户端) → NetworkCallable 带参事件 → Owner(被跟踪玩家) 收到并更新参数
  → Owner._DoSync() → RequestSerialization() → 状态同步到所有客户端
```

`SetTrackingTarget` 将所有权转移给被跟踪玩家后：
- 被跟踪玩家的 `Update()` 每帧计算跟踪数据（本地高性能）
- 控制者仍可通过 NetworkCallable 事件修改参数（事件在所有客户端执行，含 Owner）

**注意**：当前 Plane 的 `_WriteXxx()` 直接调用 `[NetworkCallable]` 方法（本地执行），非 NetworkCalling.SendCustomNetworkEvent 方式。如需让非 Owner 控制者修改参数生效，Plane 应改为通过 `NetworkCalling.SendCustomNetworkEvent` 发送事件，而非直接调用。

---

## 3. FovControlCAM — `autoMode` / `_targetFOV` 非同步字段

**现象**：`autoMode` 和 `_targetFOV` 是本地字段（非 `[UdonSynced]`），非 Owner 的 FovControlPlane 显示值可能滞后或错误。

**影响范围**：

| 字段 | Owner 写路径 | 非 Owner 如何得知 | Plane 显示 |
|------|------------|-----------------|-----------|
| `_fov` | Continuous 自动同步 | `OnDeserialization` | ✅ 正确（已修复 `_RefreshSlider(_fov)`） |
| `_targetFOV` | `SetTargetFOV` / `ApplyDefectFOV` | ❌ 不同步 | ⚠️ 非 Owner Plane 显示本地旧值 |
| `autoMode` | `SetMode` / `ApplyDefectFOV` | ❌ 不同步 | ⚠️ 非 Owner Plane Toggle 显示本地旧值 |

**现有缓解**：
- 非 Owner 调用 `SetTargetFOV` / `SetMode` 被 `IsOwner` 守卫阻止 → 不会产生错误写入
- 非 Owner Plane 的 Slider 使用 `SetValueWithoutNotify` → 不会触发回写
- `_RefreshSlider` 已通过 `isWatched` 机制在 `OnDeserialization` 时更新 Slider 显示值（`_fov`）

**待决策**：是否需要将 `autoMode` 和 `_targetFOV` 纳入同步？若保持本地，非 Owner Plane 的 Toggle/目标显示将不可靠。

---

## 4. 已修复事项汇总

| # | 脚本 | 问题 | 修复方式 |
|---|------|------|---------|
| 1 | VRCTDCamera | 初始化竞态导致 Late Joiner 状态丢失 | `_initialized` 守卫 |
| 2 | VRCTDCamera | `_ApplyBoundaryChecks` 非 Owner 写入 `[UdonSynced]` | 改为返回局部安全值 |
| 3 | VRCTDCamera | `_playerID` 写入前缺少有效性校验 | `_StartChanger` 中增加 `GetPlayerById` 校验 |
| 4 | FovControlCAM | `OnDeserialization` 快照使非 Owner 插值失效 | 仅首次快照，之后 Update 插值 |
| 5 | FovControlCAM | `Start()` 无差别覆盖已同步 `_fov` | `_fov > 0f` 检测保留已同步值 |
| 6 | FovControlCAM | 非 Owner Plane 不更新 | `isWatched` + `OnDeserialization` → `_RefreshSlider` |
| 7 | FovControlCAM | 所有权转移后 Plane 不更新 | `OnOwnershipTransferred` → `_RefreshSlider` |
| 8 | CameraControlCAM | Slider 拖拽高频 `RequestSerialization` | 0.5s 冷却 + 延迟刷新机制 |
| 9 | CameraControlCAM | `FieldChangeCallback` 空壳 | 清理空 `if` 块 |
| 10 | TrackingPlayerCAM | `Update()` 使用世界空间 | 改为 `localPosition/localRotation` + 世界→本地转换 |
| 11 | CameraOutputSystem | 非 Owner 在 `OnTransitionComplete` 写 synced + 排队 | `IsOwner` 守卫分离 Owner/非Owner 逻辑 |
| 12 | CameraOutputSystem | 纹理索引缺少下界检查 | `>= 0` 条件 |
| 13 | CameraOutputSystem | 过渡中重复请求相同目标空转 | 比较 `_nextTexIndex` 替代 `_currentTexIndex` |
| 14 | CameraOutputSystem | `_InitStaticDisplays` 未检查 null texture | 增加 `mainTex == null` 守卫 |

---

> 📌 本文档随审查推进持续更新。已修复项移入 §3，待处理项保留在 §1-§2。
