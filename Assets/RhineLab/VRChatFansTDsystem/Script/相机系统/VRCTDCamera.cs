
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// VRCTD 单相机实例 — 组件注册中心 + Manual 网络同步。
    /// 持有所有子系统所需组件的 Inspector 直接引用，
    /// 接收 SystemControl 的参数指令，管理 Camera 生命周期和点位子系统。
    /// 
    /// 变量组织（CAM 脚本 = 区域 1+2+3，无 UI 依赖）：
    ///   区域 1: 组件注册 — Inspector 直接引用的其他组件/脚本
    ///   区域 2: 同步变量 — [UdonSynced] 网络同步字段
    ///   区域 3: 可设置属性 — Inspector 配置项 + 运行时私有状态
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class VRCTDCamera : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        [Header("核心组件")]
        /// <summary>主渲染相机</summary>
        public Camera mainCamera;
        /// <summary>相机 Transform（CameraTransform）</summary>
        public Transform cameraTransform;
        /// <summary>跟踪目标 Transform（TrackingTarget）</summary>
        public Transform trackingTarget;
        /// <summary>输出 RenderTexture</summary>
        public RenderTexture renderTexture;

        [Header("显示器")]
        /// <summary>缩略图显示器材质数组</summary>
        public MeshRenderer[] displayMaterials;

        [Header("子系统与 CAM 引用")]
        /// <summary>点位子系统数组</summary>
        public SubControlSystem[] subsystems;
        /// <summary>FOV 执行引用</summary>
        public FovControlCAM fovControlCAM;
        /// <summary>位姿微调执行引用</summary>
        public CameraControlCAM cameraControlCAM;
        /// <summary>玩家跟踪执行引用</summary>
        public TrackingPlayerCAM trackingPlayerCAM;

        // ==========================================
        // 区域 2: 同步变量 — [UdonSynced] 网络同步字段
        // ==========================================

        /// <summary>当前激活的子系统索引（0=关闭，[0, subsystems.Length]）</summary>
        [UdonSynced] private int _voidNameID;

        /// <summary>当前锚点索引（[0, currentSubsystem.Targets.Length-1]）</summary>
        [UdonSynced] private int _cameraTrackingTarget;

        /// <summary>跟踪目标玩家的 VRCPlayerApi.playerId</summary>
        [UdonSynced] private int _playerID;

        /// <summary>缓动开关</summary>
        [UdonSynced] private bool _slarp;

        /// <summary>关联对象激活状态</summary>
        [UdonSynced] private bool _voidObjectActive;

        // ==========================================
        // 区域 3: 可设置属性
        // ==========================================

        [Header("调试")]
        /// <summary>调试模式开关（控制日志输出）</summary>
        public bool debugMode;

        // ---- Inspector 配置项 ----

        // （当前阶段无额外 Inspector 配置项）

        // ---- 运行时私有状态 ----

        /// <summary>当前激活的子系统引用（运行时计算，0 = 无激活子系统）</summary>
        private SubControlSystem _currentSubsystem;

        /// <summary>满帧率标志（由 SystemControl 通过 SetFullFrameRate 设置）</summary>
        private bool _fullFrameRate;

        /// <summary>初始化就绪标志（阻止 OnDeserialization 在 Start() 完成前生效）</summary>
        private bool _initialized;

        // ==========================================
        // 公共属性 — 同步变量读取接口
        // ==========================================

        /// <summary>当前激活的子系统索引（0=关闭）</summary>
        public int VoidNameID { get { return _voidNameID; } }

        /// <summary>当前锚点索引</summary>
        public int CameraTrackingTarget { get { return _cameraTrackingTarget; } }

        /// <summary>跟踪目标玩家 ID</summary>
        public int PlayerID { get { return _playerID; } }

        /// <summary>缓动开关状态</summary>
        public bool Slarp { get { return _slarp; } }

        /// <summary>关联对象激活状态</summary>
        public bool VoidObjectActive { get { return _voidObjectActive; } }

        /// <summary>当前激活的子系统（null = 无激活子系统 / voidNameID=0）</summary>
        public SubControlSystem CurrentSubsystem { get { return _currentSubsystem; } }

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // 1. 验证 Inspector 引用完整性
            _ValidateReferences();

            // 2. 向所有子系统注入 VRCTDCamera 引用
            _InitializeSubsystems();

            // 3. 标记初始化就绪（防止 OnDeserialization 在 subsystems 赋值前被 VRChat 自动触发导致状态误清零）
            _initialized = true;

            // 4. 应用初始同步状态（首次启动）
            OnDeserialization();

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[VRCTDCamera] Start 完成 — subsystems: {0}, voidNameID: {1}",
                    subsystems != null ? subsystems.Length : 0,
                    _voidNameID
                ));
            }
        }

        // ==========================================
        // VRChat 网络同步回调
        // ==========================================

        /// <summary>
        /// 同步变量反序列化回调。
        /// 由 VRChat 在以下时机自动调用：
        ///   - 场景加载 / 玩家加入（同步变量的初始状态送达）
        ///   - 所有者调用 RequestSerialization() 后其他客户端收到新状态
        ///   - Start() 末尾手动调用（应用初始状态）
        ///
        /// 执行流程：边界检查 → 激活子系统
        /// </summary>
        public override void OnDeserialization()
        {
            // 初始化未完成时跳过：subsystems 尚未赋值，VRChat 提前推送的同步状态会暂存在 [UdonSynced]
            // 字段中，由 Start() 末尾手动调用本方法时统一应用
            if (!_initialized) return;

            // 计算本地安全值（仅修正局部变量，不回写 [UdonSynced] 字段，避免非 Owner 静默写入）
            int safeVoidNameID = _ApplyBoundaryChecks();
            _CallCamera(safeVoidNameID);
        }

        // ==========================================
        // 公共方法 — SystemControl 调用接口
        // ==========================================

        /// <summary>
        /// 接收 SystemControl 指令，写入同步变量并触发网络序列化。
        /// 调用前需确保本地已通过 Networking.SetOwner 获取所有权。
        ///
        /// 参数边界检查在写入时执行（Clamp），反序列化时二次校验。
        /// </summary>
        /// <param name="voidNameID">子系统索引（0=关闭，[0, subsystems.Length]）</param>
        /// <param name="trackingTarget">锚点索引</param>
        /// <param name="playerID">跟踪目标玩家 ID</param>
        /// <param name="slarp">缓动开关</param>
        /// <param name="voidObjectActive">关联对象激活</param>
        public void _StartChanger(int voidNameID, int trackingTarget, int playerID, bool slarp, bool voidObjectActive)
        {
            // 所有权检查与获取
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            // 写入前边界检查（Clamp 到有效范围）
            int maxIndex = subsystems != null ? subsystems.Length : 0;
            _voidNameID = Mathf.Clamp(voidNameID, 0, maxIndex);

            // 根据当前子系统动态计算锚点最大索引
            int maxTargets = 0;
            if (_voidNameID > 0 && _voidNameID <= maxIndex)
            {
                SubControlSystem sub = subsystems[_voidNameID - 1];
                if (sub != null && sub.Targets != null)
                {
                    maxTargets = sub.Targets.Length - 1;
                }
            }
            _cameraTrackingTarget = Mathf.Clamp(trackingTarget, 0, maxTargets);

            // playerID 写入前有效性校验（与 _voidNameID / _cameraTrackingTarget 一致，写入前边界检查）
            int safePlayerID = playerID;
            if (safePlayerID != 0)
            {
                VRCPlayerApi p = VRCPlayerApi.GetPlayerById(safePlayerID);
                if (p == null || !p.IsValid())
                {
                    if (debugMode) Debug.LogWarning(string.Format(
                        "[VRCTDCamera] _StartChanger — playerID {0} 无效，回落到 0", safePlayerID
                    ));
                    safePlayerID = 0;
                }
            }
            _playerID = safePlayerID;
            _slarp = slarp;
            _voidObjectActive = voidObjectActive;

            // Manual 同步：写入后必须调用 RequestSerialization
            RequestSerialization();

            // 本地立即应用
            _CallCamera(_voidNameID);

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[VRCTDCamera] _StartChanger — voidNameID: {0}, trackingTarget: {1}, playerID: {2}, slarp: {3}",
                    _voidNameID, _cameraTrackingTarget, _playerID, _slarp
                ));
            }
        }

        /// <summary>
        /// 接收 SystemControl 的满帧率指令，控制 Camera.enabled。
        /// 当 SystemControl 判定本相机需要满帧率运行时调用此方法。
        /// </summary>
        /// <param name="enable">true=满帧率运行，false=可进入缩略图模式</param>
        public void SetFullFrameRate(bool enable)
        {
            _fullFrameRate = enable;
            if (mainCamera != null)
            {
                mainCamera.enabled = enable;
            }
        }

        /// <summary>
        /// 被 SystemControl 的缩略图循环调用。
        /// 开启 Camera 渲染一帧到 RenderTexture 后自动关闭。
        /// 仅在非满帧率模式下生效。
        /// </summary>
        public void _RefreshRenderFrame()
        {
            // 满帧率模式下不需要缩略图（Camera 已持续开启）
            if (_fullFrameRate) return;
            if (mainCamera == null) return;

            mainCamera.enabled = true;
            // 延迟一帧后关闭 — 使用 _DisableCameraAfterFrame 事件
            SendCustomEventDelayedFrames("_DisableCameraAfterFrame", 1);
        }

        /// <summary>
        /// 缩略图渲染一帧后关闭 Camera。
        /// 仅在仍处于非满帧率模式时关闭，防止覆盖 SetFullFrameRate(true)。
        /// </summary>
        public void _DisableCameraAfterFrame()
        {
            if (!_fullFrameRate && mainCamera != null)
            {
                mainCamera.enabled = false;
            }
        }

        // ==========================================
        // 内部方法 — 私有
        // ==========================================

        /// <summary>
        /// 验证关键 Inspector 引用的完整性（调试模式下输出警告）。
        /// </summary>
        private void _ValidateReferences()
        {
            if (!debugMode) return;

            if (mainCamera == null) Debug.LogWarning("[VRCTDCamera] mainCamera 未在 Inspector 中分配");
            if (cameraTransform == null) Debug.LogWarning("[VRCTDCamera] cameraTransform 未在 Inspector 中分配");
            if (fovControlCAM == null) Debug.LogWarning("[VRCTDCamera] fovControlCAM 未在 Inspector 中分配");
            if (cameraControlCAM == null) Debug.LogWarning("[VRCTDCamera] cameraControlCAM 未在 Inspector 中分配");
            if (trackingPlayerCAM == null) Debug.LogWarning("[VRCTDCamera] trackingPlayerCAM 未在 Inspector 中分配");
            if (subsystems == null || subsystems.Length == 0) Debug.LogWarning("[VRCTDCamera] subsystems 数组为空");
        }

        /// <summary>
        /// 向所有子系统注入 VRCTDCamera 引用。
        /// 子系统通过 _camera 引用获取 Camera、Transform、RenderTexture 等组件，
        /// 不再通过 transform.parent.Find(...) 查找。
        /// </summary>
        private void _InitializeSubsystems()
        {
            if (subsystems == null) return;

            for (int i = 0; i < subsystems.Length; i++)
            {
                if (subsystems[i] != null)
                {
                    subsystems[i]._SetCamera(this);
                }
            }
        }

        /// <summary>
        /// 同步变量边界检查 — 仅返回经修正的本地安全值，不回写 [UdonSynced] 字段。
        ///
        /// 不回写原因：OnDeserialization 在所有客户端触发，非 Owner 写入 [UdonSynced] 字段
        /// 会在下次 Owner 序列化到达时被静默覆盖（Manual 同步模式规则）。
        ///
        /// cameraTrackingTarget 和 playerID 的边界由 SubControlSystem._ChangerTarget() /
        /// TrackingPlayerCAM 各自内部防御，此处仅做调试日志。
        /// </summary>
        /// <returns>经边界检查修正后的安全 voidNameID（[0, subsystems.Length]）</returns>
        private int _ApplyBoundaryChecks()
        {
            int maxSubsystems = subsystems != null ? subsystems.Length : 0;
            int safeID = _voidNameID;

            // voidNameID 边界检查：[0, subsystems.Length]，越界时本地回落到 0（关闭）
            if (safeID < 0 || safeID > maxSubsystems)
            {
                if (debugMode) Debug.LogWarning(string.Format(
                    "[VRCTDCamera] _voidNameID 越界 ({0})，本地回落到 0", safeID
                ));
                safeID = 0;
            }

            // 其余边界检查仅做调试日志，由各自目标模块内部二次防御
            if (debugMode)
            {
                int maxTargets = 0;
                if (safeID > 0 && safeID <= maxSubsystems)
                {
                    SubControlSystem sub = subsystems[safeID - 1];
                    if (sub != null && sub.Targets != null)
                        maxTargets = sub.Targets.Length - 1;
                }
                if (_cameraTrackingTarget < 0 || _cameraTrackingTarget > maxTargets)
                {
                    Debug.LogWarning(string.Format(
                        "[VRCTDCamera] _cameraTrackingTarget 越界 ({0})，maxTargets={1}",
                        _cameraTrackingTarget, maxTargets
                    ));
                }

                if (_playerID != 0)
                {
                    VRCPlayerApi player = VRCPlayerApi.GetPlayerById(_playerID);
                    if (player == null || !player.IsValid())
                    {
                        Debug.LogWarning(string.Format(
                            "[VRCTDCamera] playerID {0} 无效 — 目标玩家不存在或已离开",
                            _playerID
                        ));
                    }
                }
            }

            return safeID;
        }

        /// <summary>
        /// 根据 voidNameID 激活对应子系统并通知其切换点位。
        ///
        /// voidNameID 映射：
        ///   0 → 关闭所有子系统（_currentSubsystem = null）
        ///   1..N → 激活 subsystems[voidNameID - 1]
        /// </summary>
        /// <param name="voidNameID">子系统索引（0=关闭，[1, subsystems.Length] 对应数组 [0..N-1]）</param>
        private void _CallCamera(int voidNameID)
        {
            // 先停用所有子系统
            if (subsystems != null)
            {
                for (int i = 0; i < subsystems.Length; i++)
                {
                    if (subsystems[i] != null)
                    {
                        subsystems[i].gameObject.SetActive(false);
                    }
                }
            }

            // 根据 voidNameID 激活目标子系统
            if (voidNameID > 0 && subsystems != null && voidNameID <= subsystems.Length)
            {
                _currentSubsystem = subsystems[voidNameID - 1];
            }
            else
            {
                _currentSubsystem = null;
            }

            // 激活子系统并触发点位切换
            if (_currentSubsystem != null)
            {
                _currentSubsystem.gameObject.SetActive(true);
                _currentSubsystem._ChangerTarget();
            }
        }

        // ==========================================
        // VRChat 事件 — 所有权转移
        // ==========================================

        /// <summary>
        /// 所有权转移回调 — VRChat 自动调用。
        /// 当对象所有权转移到新玩家时触发。
        /// </summary>
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[VRCTDCamera] 所有权转移 → {0} (ID: {1})",
                    player != null ? player.displayName : "null",
                    player != null ? player.playerId : -1
                ));
            }
        }
    }

}

