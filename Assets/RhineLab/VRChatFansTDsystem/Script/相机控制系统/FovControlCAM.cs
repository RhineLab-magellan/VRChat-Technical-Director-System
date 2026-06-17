
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// FOV 执行系统 — Continuous 网络同步。
    ///
    /// 三种模式：
    ///   - 自动模式（autoMode=true）： FOV += -rate * multiplier * deltaTime（手柄/键盘持续输入）
    ///   - 手动模式（autoMode=false）：FOV += (target - FOV) * bufferValue * deltaTime（缓冲移动到目标）
    ///   - 非 Owner 模式（!IsOwner）：   本地 FOV 插值跟踪同步值 _fov
    ///
    /// 接收子系统预设 FOV 推送（SubControlSystem → ApplyDefectFOV）。
    ///
    /// 变量组织（CAM 脚本 = 区域 1+2+3）：
    ///   区域 1: 组件注册 — Camera / FovControlPlane 引用
    ///   区域 2: 同步变量 — [UdonSynced] _fov（Continuous）
    ///   区域 3: 可设置属性 — 限制/速率/缓冲/模式配置
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class FovControlCAM : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        /// <summary>关联的 Camera（通常由 VRCTDCamera 的 mainCamera 在 Inspector 中引用）</summary>
        public Camera targetCamera;

        /// <summary>FOV 控制面板引用（可选，用于 ApplyDefectFOV 时同步更新 UI）</summary>
        public FovControlPlane fovPlane;

        // ==========================================
        // 区域 2: 同步变量 — Continuous 同步
        // ==========================================

        /// <summary>
        /// 当前 FOV 值（Continuous 同步）。
        /// Owner 在 Update() 中写入，VRChat 自动高频同步，无需 RequestSerialization。
        /// </summary>
        [UdonSynced, FieldChangeCallback(nameof(SyncedFOV))]
        private float _fov;

        /// <summary>
        /// _fov 的 FieldChangeCallback。
        /// Continuous 同步下每次网络更新到达时触发。
        /// 非 Owner 的平滑插值由 Update() 全权处理，此处不做额外操作。
        /// </summary>
        public float SyncedFOV
        {
            get { return _fov; }
            set { _fov = value; }
        }

        // ==========================================
        // 区域 3: 可设置属性 — Inspector 配置项
        // ==========================================

        [Header("FOV 限制")]
        /// <summary>最小 FOV（度）</summary>
        public float minFOV = 1f;

        /// <summary>最大 FOV（度）</summary>
        public float maxFOV = 179f;

        [Header("自动模式参数")]
        /// <summary>自动模式下的 FOV 变化速率（度/秒）</summary>
        public float rate = 10f;

        [Header("手动模式参数")]
        /// <summary>手动模式下缓冲移动到目标值的速度系数</summary>
        public float bufferValue = 5f;

        [Header("非 Owner 插值")]
        /// <summary>非 Owner 客户端插值跟踪同步值的补偿系数</summary>
        public float compensation = 3f;

        [Header("调试")]
        public bool debugMode;

        // ---- Inspector 配置项 ----

        /// <summary>
        /// FOV 模式：false=手动模式（缓冲到目标），true=自动模式（速率持续变化）。
        /// 默认手动模式。由 FovControlPlane 的 Toggle 控制。
        /// </summary>
        public bool autoMode;

        // ---- 运行时私有状态 ----

        /// <summary>手动模式下的目标 FOV 值</summary>
        private float _targetFOV;

        /// <summary>自动模式下的变化倍率（由手柄输入/SetMultiplier 设置，范围通常 [-1, 1]）</summary>
        private float _multiplier = 1f;

        /// <summary>初始化就绪标志</summary>
        private bool _initialized;

        /// <summary>首次同步标志（仅首次 OnDeserialization 做快照，之后交给 Update 平滑插值）</summary>
        private bool _firstSync = true;

        /// <summary>手动模式到达判定阈值（度）</summary>
        private const float FOV_ARRIVE_THRESHOLD = 0.05f;

        /// <summary>
        /// Plane 是否正在监听本 CAM（由 Plane 在切换 cameraIndex 时动态控制）。
        /// true 时，CAM 在收到远程同步变更（非 Owner）或所有权转移时，
        /// 主动通知 Plane 刷新 UI 以保持显示同步。
        /// </summary>
        public bool isPlaneListening;

        // ==========================================
        // 公共属性 — 供 FovControlPlane 读取
        // ==========================================

        /// <summary>当前目标 FOV（手动模式下）</summary>
        public float TargetFOV { get { return _targetFOV; } }

        /// <summary>当前倍率（自动模式下）</summary>
        public float Multiplier { get { return _multiplier; } }

        /// <summary>当前实际 FOV</summary>
        public float CurrentFOV
        {
            get
            {
                if (targetCamera != null) return targetCamera.fieldOfView;
                return _fov;
            }
        }

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // 优先使用已同步的 _fov（VRChat 可能在 Start 前已推送，如重加入场景），
            // 否则回落到 Camera 当前值或默认 60
            if (_fov <= 0f)
            {
                // _fov 尚未被同步 → 使用本地初始值
                _fov = (targetCamera != null) ? targetCamera.fieldOfView : 60f;
            }
            _targetFOV = _fov;

            // 确保 Camera FOV 与当前 _fov 一致
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = Mathf.Clamp(_fov, minFOV, maxFOV);
            }

            // isPlaneListening 由 Plane 在 _ApplyCameraIndex() 中动态设置，此处不做静态检测

            _initialized = true;
            _firstSync = false;

            // 应用初始同步状态
            OnDeserialization();
        }

        void Update()
        {
            if (targetCamera == null) return;

            if (Networking.IsOwner(gameObject))
            {
                // ======== Owner: 计算并写入 FOV ========
                if (autoMode)
                {
                    // 自动模式 — 以速率持续变化（手柄/键盘驱动）
                    _fov += -rate * _multiplier * Time.deltaTime;
                }
                else
                {
                    // 手动模式 — 缓冲移动到目标值
                    float diff = _targetFOV - _fov;
                    if (Mathf.Abs(diff) < FOV_ARRIVE_THRESHOLD)
                    {
                        // 已到达目标，直接设为目标值避免微抖动
                        _fov = _targetFOV;
                    }
                    else
                    {
                        _fov += diff * bufferValue * Time.deltaTime;
                    }
                }

                // 钳制并应用到 Camera
                _fov = Mathf.Clamp(_fov, minFOV, maxFOV);
                targetCamera.fieldOfView = _fov;
                // Continuous 同步：写入 _fov 后 VRChat 自动序列化，无需 RequestSerialization
            }
            else
            {
                // ======== Non-Owner: 插值跟踪同步值 ========
                // 公式：localFOV += (syncedFOV - localFOV) * compensation * deltaTime
                float localFOV = targetCamera.fieldOfView;
                if (Mathf.Abs(_fov - localFOV) > FOV_ARRIVE_THRESHOLD)
                {
                    localFOV += (_fov - localFOV) * compensation * Time.deltaTime;
                    targetCamera.fieldOfView = Mathf.Clamp(localFOV, minFOV, maxFOV);
                }
            }
        }

        // ==========================================
        // 公共方法 — SubControlSystem / FovControlPlane 调用接口
        // ==========================================

        /// <summary>
        /// 接收子系统推送的预设 FOV 值。
        /// 调用链：SubControlSystem._ChangerTarget() → FovControlCAM.ApplyDefectFOV(fov)
        ///
        /// 行为：设置目标 FOV → 切换到手动模式（确保预设生效）→ 同步更新面板 Slider。
        /// </summary>
        /// <param name="defectFOV">点位预设的 FOV 值</param>
        public void ApplyDefectFOV(float defectFOV)
        {
            if (!Networking.IsOwner(gameObject))
            {
                // 非 Owner 不处理预设 FOV（预设由 Owner 的 SubControlSystem 触发）
                if (debugMode) Debug.Log("[FovControlCAM] ApplyDefectFOV — 非 Owner，忽略");
                return;
            }

            // 钳制预设值到有效范围
            _targetFOV = Mathf.Clamp(defectFOV, minFOV, maxFOV);

            // 切换到手动模式（预设 FOV 应作为目标值缓冲到达，而非被自动模式速率覆盖）
            autoMode = false;

            // 同步更新控制面板 Slider（仅当 Plane 正在监听本 CAM）
            if (isPlaneListening && fovPlane != null)
            {
                fovPlane._RefreshSlider(_targetFOV);
            }

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[FovControlCAM] ApplyDefectFOV — targetFOV={0}, 已切换到手动模式",
                    _targetFOV
                ));
            }
        }

        /// <summary>
        /// 设置 FOV 变化倍率（由手柄输入/控制面板调用）。
        /// 仅在自动模式下生效。
        /// </summary>
        /// <param name="value">倍率值（通常范围 [-1, 1]）</param>
        public void SetMultiplier(float value)
        {
            _multiplier = value;
        }

        /// <summary>
        /// 设置目标 FOV 值（由 FovControlPlane Slider 调用）。
        /// 仅在手动模式下生效；若当前为自动模式则先切换到手动模式。
        /// </summary>
        /// <param name="fov">目标 FOV（度）</param>
        public void SetTargetFOV(float fov)
        {
            if (!Networking.IsOwner(gameObject)) return;

            _targetFOV = Mathf.Clamp(fov, minFOV, maxFOV);
            autoMode = false;
        }

        /// <summary>
        /// 设置 FOV 模式（由 FovControlPlane Toggle 调用）。
        /// </summary>
        /// <param name="isAuto">true=自动模式，false=手动模式</param>
        public void SetMode(bool isAuto)
        {
            if (!Networking.IsOwner(gameObject)) return;

            autoMode = isAuto;

            // 切换到手动模式时，将当前 FOV 设为目标，避免突变
            if (!isAuto)
            {
                _targetFOV = _fov;
            }

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[FovControlCAM] SetMode — autoMode={0}", isAuto
                ));
            }
        }

        // ==========================================
        // VRChat 事件
        // ==========================================

        /// <summary>
        /// 同步变量反序列化回调。
        /// 非 Owner 在此收到 Owner 的 _fov 更新。
        /// Continuous 同步下高频触发 — 仅做最小处理，平滑插值由 Update() 完成。
        /// </summary>
        public override void OnDeserialization()
        {
            if (!_initialized) return;

            // 首次同步做快照以快速响应初始状态；之后完全由 Update() 平滑插值，
            // 避免每次同步包到达（~10Hz）都打断插值连续性导致 FOV 阶跃。
            if (_firstSync && !Networking.IsOwner(gameObject) && targetCamera != null)
            {
                targetCamera.fieldOfView = Mathf.Clamp(_fov, minFOV, maxFOV);
                _firstSync = false;
            }

            // 非 Owner 收到远程变更 → 通知 Plane 刷新 UI（仅当 Plane 正在监听本 CAM）
            if (!Networking.IsOwner(gameObject) && isPlaneListening && fovPlane != null)
            {
                fovPlane._RefreshSlider(_fov);
            }
        }

        /// <summary>
        /// 所有权转移回调。
        /// 新 Owner 接管 FOV 计算，以当前实际 FOV 作为初始同步值。
        /// </summary>
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (Networking.IsOwner(gameObject))
            {
                // 新 Owner — 以当前 Camera FOV 为基准
                if (targetCamera != null)
                {
                    _fov = targetCamera.fieldOfView;
                    _targetFOV = _fov;
                }

                // 通知 Plane 重新初始化 UI（新 Owner 接管后的参数可能与之前不同，仅当 Plane 正在监听本 CAM）
                if (isPlaneListening && fovPlane != null)
                {
                    fovPlane._RefreshSlider(_fov);
                }
            }

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[FovControlCAM] 所有权转移 → {0} (ID: {1}), fov={2}",
                    player != null ? player.displayName : "null",
                    player != null ? player.playerId : -1,
                    _fov
                ));
            }
        }
    }

}
